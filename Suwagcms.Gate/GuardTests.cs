using System.Reflection;
using Suwagcms.Contracts.Actions;
using Suwagcms.Sdk.BotRuntime.Actions;
using Suwagcms.Server.Api;
using Suwgate.Architecture.Profiles;
using Suwgate.Database.Profiles;

namespace Suwagcms.Gate;

public sealed class GuardTests
{
    [Fact]
    public void Every_guard_of_suwgate_passes() =>
        SuwgateGuards.VerifyAll(
            new GuardInputs
            {
                Production =
                [
                    typeof(ServerApp).Assembly,
                    typeof(ActionOutcome).Assembly,
                    typeof(ActionView).Assembly,
                    Assembly.Load("suwagcms"),
                ],
                Tests = [typeof(GuardTests).Assembly],
                Server = typeof(ServerApp),
            },
            new DatabaseProfile()
        );
}
