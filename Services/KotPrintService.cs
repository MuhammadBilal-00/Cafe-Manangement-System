using System.Net.Sockets;
using System.Text;
using Cafe.Data;
using Cafe.Models;
using Microsoft.EntityFrameworkCore;

namespace Cafe.Services
{
    // ── Result DTOs ──
    public record BrowserKot(string Station, string PrinterName, string Url);
    public record KotDispatchResult(bool Attempted, int NetworkPrinted, List<BrowserKot> BrowserKots, List<string> Warnings)
    {
        public static KotDispatchResult None => new(false, 0, new(), new());
    }
    public record KotTestResult(bool Ok, string Message, string? BrowserUrl);

    /// <summary>
    /// Kitchen Order Ticket (KOT) dispatcher. Routes an order's items to their station's printer
    /// (Category.KotStation → KitchenPrinter.Station, else the branch default), then either sends raw
    /// ESC/POS over TCP (Network printers) or hands back a browser-print URL (Browser printers).
    /// Printing NEVER blocks or voids the sale: a network failure is logged/queued as a warning.
    /// </summary>
    public interface IKotPrintService
    {
        /// <summary>Print one KOT per station that has items. Returns browser URLs for browser printers + warnings.</summary>
        Task<KotDispatchResult> PrintForOrderAsync(int orderId, bool isReprint = false);

        /// <summary>Send a test slip to a single printer (network) or return a browser test URL.</summary>
        Task<KotTestResult> PrintTestAsync(int printerId);

        /// <summary>Build the ESC/POS bytes for a station's ticket (also used by the test slip preview).</summary>
        byte[] BuildEscPos(string station, KitchenTicketHeader header, IEnumerable<KotLine> lines);
    }

    public record KotLine(string Name, int Quantity, string? Notes);
    public record KitchenTicketHeader(string OrderNumber, string? TableName, string ServiceType, string? Waiter, DateTime Time);
    /// <summary>Model for the browser-print KOT Razor view (80mm).</summary>
    public record KotSlipVm(string Station, KitchenTicketHeader Header, List<KotLine> Lines);

    public class KotPrintService : IKotPrintService
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<KotPrintService> _logger;
        private const int ConnectTimeoutMs = 3000;

        public KotPrintService(ApplicationDbContext db, ILogger<KotPrintService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<KotDispatchResult> PrintForOrderAsync(int orderId, bool isReprint = false)
        {
            var order = await _db.Orders
                .Include(o => o.Table)
                .Include(o => o.ServiceStaff).ThenInclude(s => s!.User)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.MenuItem).ThenInclude(m => m!.Category)
                .FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null || order.OrderItems.Count == 0) return KotDispatchResult.None;

            var header = new KitchenTicketHeader(
                order.OrderNumber, order.Table?.Name, order.ServiceType,
                order.ServiceStaff?.User?.Name, DateTime.Now);

            var printers = await _db.KitchenPrinters
                .Where(p => p.BranchId == order.BranchId && p.IsActive)
                .ToListAsync();

            var warnings = new List<string>();
            var browserKots = new List<BrowserKot>();
            int networkPrinted = 0;

            // Resolve each item to a printer; items sharing a printer are merged into one KOT.
            KitchenPrinter? Resolve(string? station)
            {
                if (!string.IsNullOrWhiteSpace(station))
                {
                    var match = printers.FirstOrDefault(p => string.Equals(p.Station, station.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (match != null) return match;
                }
                return printers.FirstOrDefault(p => p.IsDefault) ?? printers.FirstOrDefault();
            }

            var groups = order.OrderItems
                .GroupBy(oi => Resolve(oi.MenuItem?.Category?.KotStation))
                .ToList();

            foreach (var group in groups)
            {
                var printer = group.Key;
                var lines = group.Select(oi => new KotLine(
                    oi.MenuItem?.Name ?? "Item", oi.Quantity, oi.Notes)).ToList();
                var station = printer?.Station ?? group.Select(oi => oi.MenuItem?.Category?.KotStation).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "Kitchen";

                if (printer == null)
                {
                    // No printer configured for this branch — fall back to a browser KOT so it still prints.
                    var url = $"/Kitchen/Kot/{orderId}?station={Uri.EscapeDataString(station)}";
                    browserKots.Add(new BrowserKot(station, "Browser", url));
                    await LogAsync(orderId, null, station, null, "Browser", lines.Count, isReprint ? "Reprint (no printer configured)" : "No printer configured");
                    continue;
                }

                if (printer.ConnectionType.Equals("Browser", StringComparison.OrdinalIgnoreCase))
                {
                    var url = $"/Kitchen/Kot/{orderId}?printer={printer.Id}";
                    browserKots.Add(new BrowserKot(printer.Station, printer.Name, url));
                    await LogAsync(orderId, printer.Id, printer.Station, printer.Name, "Browser", lines.Count, isReprint ? "Reprint" : null);
                    continue;
                }

                // Network printer → raw ESC/POS over TCP (resilient, non-blocking).
                var bytes = BuildEscPos(station, header, lines);
                var (ok, err) = await SendTcpAsync(printer.IpAddress, printer.Port, bytes);
                if (ok)
                {
                    networkPrinted++;
                    await LogAsync(orderId, printer.Id, printer.Station, printer.Name, "Printed", lines.Count, isReprint ? "Reprint" : null);
                }
                else
                {
                    warnings.Add($"KOT to '{printer.Name}' ({printer.Station}) failed: {err}. Queued for reprint.");
                    await LogAsync(orderId, printer.Id, printer.Station, printer.Name, "Failed", lines.Count, err);
                }
            }

            return new KotDispatchResult(true, networkPrinted, browserKots, warnings);
        }

        public async Task<KotTestResult> PrintTestAsync(int printerId)
        {
            var p = await _db.KitchenPrinters.FirstOrDefaultAsync(x => x.Id == printerId);
            if (p == null) return new KotTestResult(false, "Printer not found.", null);

            if (p.ConnectionType.Equals("Browser", StringComparison.OrdinalIgnoreCase))
                return new KotTestResult(true, "Opening browser test slip…", $"/Kitchen/KotTest/{p.Id}");

            var header = new KitchenTicketHeader("TEST-0001", "T1", "DineIn", "Test Waiter", DateTime.Now);
            var lines = new List<KotLine>
            {
                new("Test Item A", 2, "no onions"),
                new("Test Item B", 1, "extra spicy · Large"),
            };
            var bytes = BuildEscPos(p.Station, header, lines);
            var (ok, err) = await SendTcpAsync(p.IpAddress, p.Port, bytes);
            await LogAsync(null, p.Id, p.Station, p.Name, ok ? "Test" : "Failed", lines.Count, ok ? "Test slip" : err);
            return ok
                ? new KotTestResult(true, $"Test slip sent to {p.Name} ({p.IpAddress}:{p.Port}).", null)
                : new KotTestResult(false, $"Could not reach {p.Name} at {p.IpAddress}:{p.Port} — {err}", null);
        }

        // ── ESC/POS ticket builder ──
        public byte[] BuildEscPos(string station, KitchenTicketHeader header, IEnumerable<KotLine> lines)
        {
            const byte ESC = 0x1B, GS = 0x1D;
            using var ms = new MemoryStream();
            void Raw(params byte[] b) => ms.Write(b, 0, b.Length);
            void Txt(string s) { var b = Encoding.ASCII.GetBytes(s); ms.Write(b, 0, b.Length); }
            void Nl(int n = 1) { for (int i = 0; i < n; i++) ms.WriteByte(0x0A); }

            Raw(ESC, 0x40);                 // initialize
            Raw(ESC, 0x61, 0x01);           // center
            Raw(GS, 0x21, 0x11);            // double width + height
            Raw(ESC, 0x45, 0x01);           // bold on
            Txt($"KITCHEN - {station.ToUpperInvariant()}"); Nl();
            Raw(GS, 0x21, 0x00);            // normal size
            Raw(ESC, 0x45, 0x00);           // bold off
            Raw(ESC, 0x61, 0x00);           // left
            Txt(new string('-', 42)); Nl();

            Txt($"Order : {header.OrderNumber}"); Nl();
            if (!string.IsNullOrWhiteSpace(header.TableName)) { Txt($"Table : {header.TableName}"); Nl(); }
            Txt($"Type  : {header.ServiceType}"); Nl();
            if (!string.IsNullOrWhiteSpace(header.Waiter)) { Txt($"Waiter: {header.Waiter}"); Nl(); }
            Txt($"Time  : {header.Time:dd MMM HH:mm}"); Nl();
            Txt(new string('-', 42)); Nl();

            foreach (var l in lines)
            {
                Raw(GS, 0x21, 0x01);        // double height (tall, easy to read)
                Raw(ESC, 0x45, 0x01);       // bold on
                Txt($"{l.Quantity} x {Clean(l.Name)}"); Nl();
                Raw(GS, 0x21, 0x00);
                Raw(ESC, 0x45, 0x00);
                if (!string.IsNullOrWhiteSpace(l.Notes)) { Txt($"   >> {Clean(l.Notes!)}"); Nl(); }
            }

            Txt(new string('-', 42)); Nl();
            Raw(ESC, 0x61, 0x01);
            Txt($"{lines.Sum(x => x.Quantity)} item(s)"); Nl(2);
            Raw(GS, 0x56, 0x42, 0x00);     // partial cut (feed 0)
            return ms.ToArray();
        }

        // ── TCP send with timeout + one retry ──
        private async Task<(bool ok, string? error)> SendTcpAsync(string? ip, int port, byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(ip)) return (false, "no IP address configured");
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                try
                {
                    using var client = new TcpClient();
                    using var cts = new CancellationTokenSource(ConnectTimeoutMs);
                    await client.ConnectAsync(ip, port, cts.Token);
                    await using var stream = client.GetStream();
                    await stream.WriteAsync(payload, cts.Token);
                    await stream.FlushAsync(cts.Token);
                    return (true, null);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "KOT TCP send to {Ip}:{Port} failed (attempt {Attempt})", ip, port, attempt);
                    if (attempt == 2) return (false, ex is OperationCanceledException ? "timeout" : ex.Message);
                }
            }
            return (false, "unreachable");
        }

        private async Task LogAsync(int? orderId, int? printerId, string? station, string? printerName, string status, int itemCount, string? message)
        {
            _db.KotPrintLogs.Add(new KotPrintLog
            {
                OrderId = orderId, KitchenPrinterId = printerId, Station = station, PrinterName = printerName,
                Status = status, ItemCount = itemCount, Message = message, PrintedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
        }

        private static string Clean(string s) => s.Replace("\n", " ").Replace("\r", " ").Trim();
    }
}
