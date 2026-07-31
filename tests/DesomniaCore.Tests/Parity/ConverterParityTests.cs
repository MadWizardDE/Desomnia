using MadWizard.Desomnia.Configuration;
using Xunit;

namespace MadWizard.Desomnia.Tests.Parity
{
    /// <summary>Pins the action config-string grammar that ALL production actions pass
    /// through (Configuration/Actions/*.cs). Phase 4 replaces this converter pipeline
    /// with eager-parsing *Info types (spec §6.2) claiming the name grammar survives —
    /// these tests are the proof obligation. The last-'+'-split change (§9.3) is the
    /// only permitted delta; the first-'+' quirk pin below flips with it.</summary>
    public class ConverterParityTests
    {
        private static object? Convert<TConverter>(string value) where TConverter : System.ComponentModel.TypeConverter, new()
            => new TConverter().ConvertFrom(null, null, value);

        [Fact]
        public void PlainNameParsesToActionInfo()
        {
            var action = Assert.IsType<ActionInfo>(Convert<ActionInfoConverter>("wake"));

            Assert.Equal("wake", action.Command!.Function);
            Assert.Null(action.Command!.Arguments);
        }

        [Fact]
        public void ArgumentListIsExtractedWithQuoteStripping()
        {
            var action = Assert.IsType<ActionInfo>(Convert<ActionInfoConverter>("notify('hello','world')"));

            Assert.Equal("notify", action.Command!.Function);
            Assert.NotNull(action.Command!.Arguments);
            Assert.Equal(2, action.Command!.Arguments!.Length);
            Assert.Equal("hello", action.Command!.Arguments[0]);
            Assert.Equal("world", action.Command!.Arguments[1]);
        }

        [Fact]
        public void NoSuffixParsesToDelayedActionInfoWithoutDelay()
        {
            var action = Assert.IsType<DelayedActionInfo>(Convert<DelayedActionInfoConverter>("wake"));

            Assert.Equal("wake", action.Command!.Function);
            Assert.False(action.HasDelay);
        }

        [Fact]
        public void DurationSuffixParsesToScheduledActionInfo()
        {
            var action = Assert.IsType<ScheduledActionInfo>(Convert<DelayedActionInfoConverter>("wake+10min"));

            Assert.Equal("wake", action.Command!.Function);
            Assert.Equal(TimeSpan.FromMinutes(10), action.Delay);
            Assert.True(action.HasDelay);
        }

        [Theory]
        [InlineData("wake+90s", 90)]
        [InlineData("wake+2h30min", 9000)]
        [InlineData("wake+500ms", 0.5)]
        public void FriendlyDurationFormsAreNormalized(string value, double seconds)
        {
            var action = Assert.IsType<ScheduledActionInfo>(Convert<DelayedActionInfoConverter>(value));

            Assert.Equal(TimeSpan.FromSeconds(seconds), action.Delay);
        }

        [Fact]
        public void TimesSuffixParsesToThrottledActionInfo()
        {
            var action = Assert.IsType<ThrottledActionInfo>(Convert<DelayedActionInfoConverter>("run+2x"));

            Assert.Equal("run", action.Command!.Function);
            Assert.Equal(2u, action.Times);
            Assert.True(action.HasDelay);
        }

        [Fact]
        public void ArgumentsAndSuffixCombine()
        {
            var action = Assert.IsType<ScheduledActionInfo>(Convert<DelayedActionInfoConverter>("cmd('a')+10s"));

            Assert.Equal("cmd", action.Command!.Function);
            Assert.Equal(TimeSpan.FromSeconds(10), action.Delay);
            Assert.Equal("a", action.Command!.Arguments![0]);
        }

        [Fact]
        public void ScheduledConverterSynthesizesZeroDelayDefault()
        {
            // "+0s" appended for suffix-less input (ScheduledActionInfo.cs:28-29) — this is
            // why SystemMonitor's onSuspendTimeout must-have-delay check can rely on
            // HasDelay (SystemMonitor.cs:69-70)
            var action = Assert.IsType<ScheduledActionInfo>(Convert<ScheduledActionInfoConverter>("wake"));

            Assert.Equal(TimeSpan.Zero, action.Delay);
            Assert.False(action.HasDelay);
        }

        [Fact]
        public void ThrottledConverterSynthesizesZeroTimesDefault()
        {
            var action = Assert.IsType<ThrottledActionInfo>(Convert<ThrottledActionInfoConverter>("wake"));

            Assert.Equal(0u, action.Times);
            Assert.False(action.HasDelay);
        }

        [Fact]
        public void NegativeDelayIsRejectedAtParseTime()
        {
            Assert.Throws<FormatException>(() => Convert<DelayedActionInfoConverter>("wake+-00:05:00"));
        }

        [Fact]
        public void ScheduledConverterRejectsTimesSuffix()
        {
            Assert.Throws<FormatException>(() => Convert<ScheduledActionInfoConverter>("wake+2x"));
        }

        [Fact]
        public void ThrottledConverterRejectsDurationSuffix()
        {
            Assert.Throws<FormatException>(() => Convert<ThrottledActionInfoConverter>("wake+10s"));
        }

        [Fact]
        public void MultiPlusSeparatorsAreRejected()
        {
            // flipped quirk (phase 4, §9.3): the old first-'+' split silently dropped
            // extra segments; '+' is reserved now and multiple separators are a config error
            Assert.Throws<FormatException>(() => Convert<DelayedActionInfoConverter>("wake+10s+5s"));
        }

        [Theory]
        [InlineData("wake+2xx")]
        [InlineData("wake+1x1x")]
        public void GarbageTimesSuffixesAreRejected(string value)
        {
            // deliberate strictness (§9.3): the old parser's Replace("x","") tolerated
            // these shapes ("+2xx" → 2, "+1x1x" → 11); the times grammar is strictly \d+x
            Assert.Throws<FormatException>(() => Convert<DelayedActionInfoConverter>(value));
        }

        [Fact]
        public void UrlActionsAreDetectedByStructure()
        {
            var action = Assert.IsType<ActionInfo>(Convert<ActionInfoConverter>("fritz://heimdail/ports/eth0?maxspeed=1000"));

            Assert.NotNull(action.URL);
            Assert.Equal("fritz", action.URL!.Scheme);
            Assert.Equal("fritz://heimdail/ports/eth0?maxspeed=1000", action.ToString());   // full string, for logging
            Assert.Null(action.Command);                      // a URL action HAS no command side
        }

        [Fact]
        public void UrlActionsComposeWithScheduleSuffixes()
        {
            var scheduled = Assert.IsType<ScheduledActionInfo>(Convert<DelayedActionInfoConverter>("fritz://box/ports/eth0?maxspeed=100+5min"));

            Assert.NotNull(scheduled.URL);
            Assert.Equal("fritz://box/ports/eth0?maxspeed=100", scheduled.URL!.OriginalString);   // suffix consumed
            Assert.Equal(TimeSpan.FromMinutes(5), scheduled.Delay);
            Assert.True(scheduled.HasDelay);

            var throttled = Assert.IsType<ThrottledActionInfo>(Convert<DelayedActionInfoConverter>("web://x/y+2x"));

            Assert.NotNull(throttled.URL);
            Assert.Equal(2u, throttled.Times);
        }

        [Fact]
        public void ColonOnlySchemesQualifyButDriveLettersDoNot()
        {
            // '//' is optional (mailto:-style); a single-letter scheme never qualifies,
            // so Windows paths keep parsing as plain names
            Assert.NotNull(Assert.IsType<ActionInfo>(Convert<ActionInfoConverter>("mailto:user@example.org")).URL);

            var path = Assert.IsType<ActionInfo>(Convert<ActionInfoConverter>(@"C:/tools/run.exe"));
            Assert.Null(path.URL);
            Assert.Equal(@"C:/tools/run.exe", path.Command!.Function);
        }

        [Fact]
        public void UrlContentIsNeverMangledByArgumentExtraction()
        {
            // parens and quotes are legal URL content — the shape test runs BEFORE
            // ExtractArguments, which would have stripped them
            var action = Assert.IsType<ActionInfo>(Convert<ActionInfoConverter>("web://host/do('a','b')"));

            Assert.NotNull(action.URL);
            Assert.Null(action.Command);
            Assert.Contains("('a','b')", action.URL!.OriginalString);
        }

        [Fact]
        public void PlainAttributesDoNotParseSuffixesOnUrls()
        {
            // ActionInfo-typed attributes never had suffix parsing — for URLs the '+'
            // stays inside the name/URL string, mirroring how names behave there
            var action = Assert.IsType<ActionInfo>(Convert<ActionInfoConverter>("web://host/a+5min"));

            Assert.NotNull(action.URL);                       // '+' is legal URL content here
            Assert.Equal("web://host/a+5min", action.URL!.OriginalString);
        }
    }
}
