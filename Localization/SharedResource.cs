namespace Cafe
{
    /// <summary>
    /// Marker type for the shared localization catalog (Resources/SharedResource.&lt;culture&gt;.resx).
    /// Views inject IStringLocalizer&lt;Cafe.SharedResource&gt; and use the English text as the key, so the
    /// default (en) needs no resx and only translated cultures (ur) supply values. Phase 10 (Urdu slice).
    ///
    /// NOTE: this class must live OUTSIDE the Resources folder. If it sits next to the .resx, the SDK's
    /// DependentUpon convention names the embedded resource "Cafe.SharedResource.*" and the localizer
    /// (ResourcesPath="Resources", which expects "Cafe.Resources.SharedResource.*") can't find it.
    /// </summary>
    public class SharedResource
    {
    }
}
