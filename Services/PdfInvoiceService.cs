using System;
using System.Linq;
using Cafe.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Cafe.Services
{
    public interface IPdfInvoiceService
    {
        /// <summary>Renders a finished invoice (with its order + items) to a PDF byte array.</summary>
        byte[] GenerateInvoicePdf(Invoice invoice, Order order, Branch branch, string? footerNote);
    }

    public class PdfInvoiceService : IPdfInvoiceService
    {
        private static string Money(decimal amount) => $"Rs. {amount:N2}";

        public byte[] GenerateInvoicePdf(Invoice invoice, Order order, Branch branch, string? footerNote)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                    page.Header().Element(c => ComposeHeader(c, invoice, branch));
                    page.Content().Element(c => ComposeContent(c, invoice, order));
                    page.Footer().Element(c => ComposeFooter(c, footerNote));
                });
            });

            return document.GeneratePdf();
        }

        private static void ComposeHeader(IContainer container, Invoice invoice, Branch branch)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        left.Item().Text(branch.Name).FontSize(18).Bold().FontColor(Colors.Brown.Darken2);
                        if (!string.IsNullOrWhiteSpace(branch.Location))
                            left.Item().Text(branch.Location).FontSize(9).FontColor(Colors.Grey.Darken1);
                        if (!string.IsNullOrWhiteSpace(branch.ContactInfo))
                            left.Item().Text(branch.ContactInfo).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(150).Column(right =>
                    {
                        right.Item().AlignRight().Text("INVOICE").FontSize(16).Bold();
                        right.Item().AlignRight().Text(invoice.InvoiceNumber).FontSize(10).FontColor(Colors.Grey.Darken2);
                        right.Item().AlignRight().Text(invoice.CreatedAt.ToString("dd MMM yyyy  HH:mm")).FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                });

                col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });
        }

        private static void ComposeContent(IContainer container, Invoice invoice, Order order)
        {
            container.PaddingVertical(10).Column(col =>
            {
                col.Spacing(10);

                // Meta row: order number, customer, payment
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(t => { t.Span("Order: ").SemiBold(); t.Span(order.OrderNumber); });
                        c.Item().Text(t => { t.Span("Customer: ").SemiBold(); t.Span(order.Customer?.Name ?? "Walk-in"); });
                    });
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().Text(t => { t.Span("Payment: ").SemiBold(); t.Span(invoice.PaymentMethod); });
                        c.Item().Text(t => { t.Span("Status: ").SemiBold(); t.Span(invoice.PaymentStatus); });
                    });
                });

                // Items table
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(5); // item
                        columns.RelativeColumn(1); // qty
                        columns.RelativeColumn(2); // unit
                        columns.RelativeColumn(2); // total
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Item");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Qty");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Unit");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                    });

                    foreach (var item in order.OrderItems)
                    {
                        var name = item.MenuItem?.Name ?? $"Item #{item.MenuItemId}";
                        table.Cell().Element(BodyCell).Text(name);
                        table.Cell().Element(BodyCell).AlignRight().Text(item.Quantity.ToString());
                        table.Cell().Element(BodyCell).AlignRight().Text(Money(item.Price));
                        table.Cell().Element(BodyCell).AlignRight().Text(Money(item.Price * item.Quantity));
                    }
                });

                // Totals panel
                col.Item().AlignRight().Width(220).Column(totals =>
                {
                    TotalLine(totals, "Subtotal", Money(invoice.Subtotal));

                    if (invoice.PromoDiscount > 0)
                        TotalLine(totals, $"Promo {(invoice.PromoCodeText != null ? $"({invoice.PromoCodeText})" : "")}", "−" + Money(invoice.PromoDiscount));

                    if (invoice.PartnershipDiscount > 0)
                        TotalLine(totals, invoice.PartnershipText ?? "Card discount", "−" + Money(invoice.PartnershipDiscount));

                    if (invoice.TaxAmount > 0)
                        TotalLine(totals, $"Tax ({invoice.TaxRate:0.##}%)", Money(invoice.TaxAmount));

                    totals.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    totals.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Total").Bold().FontSize(12);
                        row.RelativeItem().AlignRight().Text(Money(invoice.TotalAmount)).Bold().FontSize(12).FontColor(Colors.Brown.Darken2);
                    });
                });
            });

            static IContainer HeaderCell(IContainer c) =>
                c.BorderBottom(1).BorderColor(Colors.Grey.Medium).PaddingVertical(4).DefaultTextStyle(t => t.SemiBold());
            static IContainer BodyCell(IContainer c) =>
                c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4);
        }

        private static void TotalLine(ColumnDescriptor totals, string label, string value)
        {
            totals.Item().Row(row =>
            {
                row.RelativeItem().Text(label).FontColor(Colors.Grey.Darken2);
                row.RelativeItem().AlignRight().Text(value);
            });
        }

        private static void ComposeFooter(IContainer container, string? footerNote)
        {
            container.Column(col =>
            {
                col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                col.Item().PaddingTop(6).AlignCenter()
                    .Text(string.IsNullOrWhiteSpace(footerNote) ? "Thank you for your visit!" : footerNote)
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        }
    }
}
