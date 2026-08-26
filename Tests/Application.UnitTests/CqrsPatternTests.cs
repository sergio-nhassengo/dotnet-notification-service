using System.Reflection;
using FluentValidation;
using MediatR;

namespace Application.UnitTests;


public class CqrsPatternTests
{
    private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;

    public static IEnumerable<object[]> RequestTypes()
    {
        return ApplicationAssembly.GetTypes()
            .Where(IsCommandOrQuery)
            .Select(t => new object[] { t });
    }

    private static bool IsCommandOrQuery(Type type)
    {
        if (!type.Name.EndsWith("Command") && !type.Name.EndsWith("Query"))
        {
            return false;
        }

        return type.GetInterfaces().Any(i =>
            i == typeof(IBaseRequest) ||
            (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>)));
    }

    [Fact]
    public void Application_assembly_contains_at_least_one_command_and_one_query()
    {
        var requestTypes = ApplicationAssembly.GetTypes().Where(IsCommandOrQuery).ToList();

        Assert.Contains(requestTypes, t => t.Name.EndsWith("Command"));
        Assert.Contains(requestTypes, t => t.Name.EndsWith("Query"));
    }

    [Theory]
    [MemberData(nameof(RequestTypes))]
    public void Command_or_query_lives_under_the_matching_Commands_or_Queries_namespace(Type requestType)
    {
        var expectedSegment = requestType.Name.EndsWith("Command") ? ".Commands." : ".Queries.";

        Assert.Contains(expectedSegment, $".{requestType.Namespace}.");
    }

    [Theory]
    [MemberData(nameof(RequestTypes))]
    public void Command_or_query_has_a_matching_handler_in_the_same_namespace(Type requestType)
    {
        var expectedHandlerName = requestType.Name + "Handler";

        var handlerType = ApplicationAssembly.GetTypes()
            .SingleOrDefault(t => t.Name == expectedHandlerName && t.Namespace == requestType.Namespace);

        Assert.True(handlerType is not null,
            $"Expected a handler named '{expectedHandlerName}' in namespace '{requestType.Namespace}' for request '{requestType.Name}'.");

        var implementsHandlerInterface = handlerType!.GetInterfaces().Any(i =>
            i.IsGenericType &&
            (i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) || i.GetGenericTypeDefinition() == typeof(IRequestHandler<>)) &&
            i.GetGenericArguments()[0] == requestType);

        Assert.True(implementsHandlerInterface,
            $"'{handlerType.FullName}' does not implement IRequestHandler<{requestType.Name}[, TResponse]>.");
    }

    [Theory]
    [MemberData(nameof(RequestTypes))]
    public void Validator_for_a_command_or_query_if_present_is_named_and_typed_consistently(Type requestType)
    {
        var expectedValidatorName = requestType.Name + "Validator";

        var validatorType = ApplicationAssembly.GetTypes()
            .SingleOrDefault(t => t.Namespace == requestType.Namespace &&
                                   t.Name.EndsWith("Validator") &&
                                   typeof(IValidator).IsAssignableFrom(t));

        
        if (validatorType is null)
        {
            return;
        }

        Assert.Equal(expectedValidatorName, validatorType.Name);

        var baseType = validatorType.BaseType;
        Assert.True(baseType is { IsGenericType: true } && baseType.GetGenericArguments()[0] == requestType,
            $"'{validatorType.FullName}' does not derive from AbstractValidator<{requestType.Name}>.");
    }
}
