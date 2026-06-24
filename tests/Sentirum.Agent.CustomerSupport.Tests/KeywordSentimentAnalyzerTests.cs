using System.Threading.Tasks;
using FluentAssertions;
using Sentirum.Agent.CustomerSupport.Sentiment;
using Xunit;

namespace Sentirum.Agent.CustomerSupport.Tests;

public class KeywordSentimentAnalyzerTests
{
    private readonly KeywordSentimentAnalyzer _analyzer = KeywordSentimentAnalyzer.Instance;

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyOrWhitespace_IsNeutralZero(string text)
    {
        var score = await _analyzer.AnalyzeAsync(text);

        score.Label.Should().Be(SentimentLabel.Neutral);
        score.Polarity.Should().Be(0);
    }

    [Fact]
    public async Task PositiveWords_ProducePositivePolarity()
    {
        var score = await _analyzer.AnalyzeAsync("This is great, I'm very happy, thanks!");

        score.Polarity.Should().BeGreaterThan(0);
        score.Label.Should().Be(SentimentLabel.Positive);
        score.IsNegative.Should().BeFalse();
    }

    [Fact]
    public async Task StronglyNegative_ClearsDefaultEscalationThreshold()
    {
        // Default escalation threshold is -0.3. A short, angry message must
        // land below it so the escalation trigger fires.
        var score = await _analyzer.AnalyzeAsync("This is terrible and broken, I'm furious!");

        score.Polarity.Should().BeLessThan(-0.3);
        score.Label.Should().Be(SentimentLabel.Negative);
        score.IsNegative.Should().BeTrue();
    }

    [Fact]
    public async Task Negation_FlipsPositiveToNegative()
    {
        // "not happy" — the negation must invert the positive term.
        var negated = await _analyzer.AnalyzeAsync("I am not happy with this");
        negated.Polarity.Should().BeLessThan(0, "negation should flip 'happy' negative");
    }

    [Fact]
    public async Task TurkishNegativeWords_AreRecognised()
    {
        var score = await _analyzer.AnalyzeAsync("Ürün berbat geldi, çok kızgınım");

        score.Polarity.Should().BeLessThan(0);
        score.Label.Should().Be(SentimentLabel.Negative);
    }

    [Theory]
    [InlineData("What are your business hours today?")]
    [InlineData("I'd like to know the shipping options")]
    public async Task NeutralText_IsNeutral(string text)
    {
        var score = await _analyzer.AnalyzeAsync(text);

        score.Polarity.Should().Be(0, "no sentiment-bearing terms are present");
        score.Label.Should().Be(SentimentLabel.Neutral);
    }

    [Fact]
    public async Task Polarity_IsAlwaysClampedToUnitRange()
    {
        // Even an extreme string cannot escape [-1, 1].
        var score = await _analyzer.AnalyzeAsync("furious terrible awful horrible worst disgusting fraud");

        score.Polarity.Should().BeInRange(-1, 1);
    }

    // ── Regression tests for fixed bugs ──────────────────────────────────

    [Fact]
    public async Task B1_NegationWindow_SurvivesIntensifier()
    {
        // "not <intensifier> <positive>" must stay negative — the negation
        // window (3) must not reset on the intervening non-sentiment word.
        var score = await _analyzer.AnalyzeAsync("I am not very happy with this order");

        score.Polarity.Should().BeLessThan(0, "'not very happy' should be negative");
        score.IsNegative.Should().BeTrue();
    }

    [Fact]
    public async Task B1_NegationWindow_ExpiresAcrossLongDistance()
    {
        // A negation far from a sentiment word must NOT flip it.
        var score = await _analyzer.AnalyzeAsync(
            "I did not order that, but overall the product is great");

        score.Polarity.Should().BeGreaterThan(0, "'great' is far from 'not' and must stay positive");
    }

    [Fact]
    public async Task B2_Never_IsNegativeWordInSupportContext()
    {
        // "never" signals a problem in complaints → negative (not a negator
        // that flips the following word).
        var score = await _analyzer.AnalyzeAsync("I never received my package, this is awful");

        score.Polarity.Should().BeLessThan(0);
        score.IsNegative.Should().BeTrue();
    }

    [Theory]
    [InlineData("ürün kayıp geldi")]   // lowercase Turkish
    [InlineData("ÜRÜN KAYIP GELDİ")]   // uppercase Turkish (İ marker present)
    [InlineData("KARGO KAYIP")]        // uppercase Turkish, no marker — the hard case
    public async Task B3_TurkishDotlessI_FoldsAcrossCasing(string text)
    {
        // Uppercase Turkish with dotless-i (ı) must match the lowercase
        // lexicon entry after the ı→i fold.
        var score = await _analyzer.AnalyzeAsync(text);

        score.Polarity.Should().BeLessThan(0, $"'{text}' should be negative via 'kayıp'");
        score.Label.Should().Be(SentimentLabel.Negative);
    }
}
