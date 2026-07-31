using Porta.Core.Indexing;

namespace Porta.Core.Tests.Indexing;

public class IgnoreRulesTests
{
    [Theory]
    [InlineData("node_modules", "node_modules/x.js", true)]
    [InlineData("node_modules", "a/b/node_modules/x.js", true)]
    [InlineData("node_modules", "nodes/x.js", false)]
    [InlineData("*.tmp", "a.tmp", true)]
    [InlineData("*.tmp", "dir/b.tmp", true)]
    [InlineData("*.tmp", "a.tmpx", false)]
    [InlineData(".porta/", ".porta/versions/old~1.txt", true)]
    [InlineData(".porta/", "porta/keep.txt", false)]
    public void Name_patterns_match_any_segment(string pattern, string path, bool ignored)
    {
        Assert.Equal(ignored, new IgnoreRules([pattern]).IsIgnored(path));
    }

    [Theory]
    [InlineData("/build/", "build/out.js", true)]
    [InlineData("/build/", "build", true)]
    [InlineData("/build/", "src/build/out.js", false)] // привязано к корню
    [InlineData("docs/*.bak", "docs/a.bak", true)]
    [InlineData("docs/*.bak", "docs/sub/a.bak", false)] // * не переходит '/'
    [InlineData("docs/*.bak", "x/docs/a.bak", false)]   // привязано к корню
    public void Path_patterns_are_root_anchored(string pattern, string path, bool ignored)
    {
        Assert.Equal(ignored, new IgnoreRules([pattern]).IsIgnored(path));
    }

    [Fact]
    public void Double_star_crosses_segments_in_path_patterns()
    {
        var rules = new IgnoreRules(["cache/**/tmp"]);

        Assert.True(rules.IsIgnored("cache/a/b/tmp"));
        Assert.True(rules.IsIgnored("cache/a/b/tmp/inside.txt"));
    }

    [Fact]
    public void Comments_and_blank_lines_are_skipped()
    {
        var rules = new IgnoreRules(["# comment", "   ", "*.log"]);

        Assert.True(rules.IsIgnored("app.log"));
        Assert.False(rules.IsIgnored("comment"));
    }

    [Fact]
    public void Empty_rules_ignore_nothing()
    {
        Assert.False(IgnoreRules.Empty.IsIgnored("anything/here.txt"));
    }
}
