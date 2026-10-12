using Aw.Judge.Core.Secrets;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Secrets;

[Lane(TestLane.Unit)]
public sealed class SecretNamesTests
{
    [Fact]
    public void Every_secret_has_its_variable_and_the_list_names_each_once() =>
        Assert.Equal(Enum.GetValues<SecretName>().Select(SecretNames.Variable), SecretNames.Variables, StringComparer.Ordinal);

    [Fact]
    public void The_TypeSafe_key_is_read_from_its_variable() =>
        Assert.Equal("TYPESAFE_API_KEY", SecretNames.Variable(SecretName.TypeSafeApiKey));
}
