using Suwagcms.Sdk.Format.Versions;

namespace Suwagcms.Sdk.Tests.Format.Versions;

public sealed class VersionDigestTests
{
    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-4")]
    public void The_digest_ignores_member_order_white_space_and_its_own_member()
    {
        var plain = VersionDigest.Of("""{"formatVersion":2,"bot":"b","number":1,"facts":[{"name":"x"}]}""");
        var shuffled = VersionDigest.Of(
            """
            { "facts": [ { "name": "x" } ], "number": 1, "digest": "whatever", "bot": "b", "formatVersion": 2 }
            """
        );

        Assert.Equal(plain, shuffled);
        Assert.Matches("^[0-9a-f]{64}$", plain);
    }

    [Fact]
    [Trait("criterion", "AC-bot-runtime-sdk-4")]
    public void The_digest_changes_with_the_content_and_the_order_of_a_list()
    {
        var one = VersionDigest.Of("""{"facts":[{"name":"a"},{"name":"b"}]}""");

        Assert.NotEqual(one, VersionDigest.Of("""{"facts":[{"name":"b"},{"name":"a"}]}"""));
        Assert.NotEqual(one, VersionDigest.Of("""{"facts":[{"name":"a"},{"name":"c"}]}"""));
    }
}
