using MadWizard.Desomnia.Display.Configuration.Converter;
using System.ComponentModel;

namespace MadWizard.Desomnia.Display.Configuration
{
    /// <summary>
    /// Whether a watched display counts as demand (keeps the system from idling), and when.
    /// Applies per <c>&lt;Display&gt;</c> or, as the default, on the whole <c>&lt;DisplayMonitor&gt;</c>.
    /// </summary>
    [TypeConverter(typeof(PreventIdleTypeConverter))]
    public enum PreventIdleType
    {
        /// <summary>Never creates demand — the display is watched for events only.</summary>
        Never,

        /// <summary>Always creates demand while connected, regardless of the display's power state.</summary>
        Always,

        /// <summary>
        /// Creates demand only while the display is enabled (powered on). A display whose power
        /// state is unknown — HDMI sinks that hold the link while switched off — still counts;
        /// only a positively known-off display is dropped.
        /// </summary>
        Enabled,
    }
}
