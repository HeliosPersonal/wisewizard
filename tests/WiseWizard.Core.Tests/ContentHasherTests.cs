using WiseWizard.Core.Services;

namespace WiseWizard.Core.Tests;

public class ContentHasherTests
{
    [Fact]
    public void Compute_is_deterministic_for_same_input()
    {
        var a = ContentHasher.Compute("Title", "Body content", "https://x/1");
        var b = ContentHasher.Compute("Title", "Body content", "https://x/1");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Compute_returns_lowercase_hex_sha256()
    {
        var hash = ContentHasher.Compute("Title", "Body");

        Assert.Equal(64, hash.Length);
        Assert.All(hash, c => Assert.True(char.IsDigit(c) || (c >= 'a' && c <= 'f')));
    }

    [Fact]
    public void Compute_differs_for_different_content()
    {
        var a = ContentHasher.Compute("Title", "Body one");
        var b = ContentHasher.Compute("Title", "Body two");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Compute_normalizes_whitespace_and_case()
    {
        var a = ContentHasher.Compute("  Hello   World ", "The   Body");
        var b = ContentHasher.Compute("hello world", "the body");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Compute_treats_null_and_empty_and_whitespace_as_same()
    {
        var fromNull = ContentHasher.Compute(null, null, null);
        var fromEmpty = ContentHasher.Compute("", "", "");
        var fromWhitespace = ContentHasher.Compute("   ", "\t", "\n");

        Assert.Equal(fromNull, fromEmpty);
        Assert.Equal(fromNull, fromWhitespace);
    }

    [Fact]
    public void Compute_url_participates_in_hash()
    {
        var a = ContentHasher.Compute("Title", "Body", "https://x/1");
        var b = ContentHasher.Compute("Title", "Body", "https://x/2");

        Assert.NotEqual(a, b);
    }
}
