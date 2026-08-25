namespace FeatureCli;

internal static class FeatureTemplates
{
    public static (string FileName, string Content)[] Query(
        string name, string feature, string entity, string rootNamespace, string dbContextNamespace, string dbContextType) =>
        new (string, string)[]
        {
            ($"{name}Query.cs", $$"""
            using MediatR;

            namespace {{rootNamespace}}.Features.{{feature}}.Queries.{{name}};

            // TODO: add query parameters, e.g. public record {{name}}Query(int Id) : IRequest<{{name}}Response>;
            public record {{name}}Query : IRequest<{{name}}Response>;

            """),
            ($"{name}QueryHandler.cs", $$"""
            using {{dbContextNamespace}};
            using MediatR;

            namespace {{rootNamespace}}.Features.{{feature}}.Queries.{{name}};

            public class {{name}}QueryHandler({{dbContextType}} context) : IRequestHandler<{{name}}Query, {{name}}Response>
            {
                public Task<{{name}}Response> Handle({{name}}Query request, CancellationToken cancellationToken)
                {
                    // TODO: query context.{{feature}} (Domain.Entities.{{entity}}) and map the result to {{name}}Response
                    throw new NotImplementedException();
                }
            }

            """),
            ($"{name}Response.cs", $$"""
            namespace {{rootNamespace}}.Features.{{feature}}.Queries.{{name}};

            public class {{name}}Response
            {
                // TODO: define the properties returned to the caller
            }

            """)
        };

    public static (string FileName, string Content)[] Command(
        string name, string feature, string entity, string rootNamespace, string dbContextNamespace, string dbContextType) =>
        new (string, string)[]
        {
            ($"{name}Command.cs", $$"""
            using MediatR;

            namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{name}};

            // TODO: add command parameters, e.g. public record {{name}}Command(int Id, string Title) : IRequest;
            public record {{name}}Command : IRequest;

            """),
            ($"{name}CommandHandler.cs", $$"""
            using {{dbContextNamespace}};
            using MediatR;

            namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{name}};

            public class {{name}}CommandHandler({{dbContextType}} context) : IRequestHandler<{{name}}Command>
            {
                public Task Handle({{name}}Command request, CancellationToken cancellationToken)
                {
                    // TODO: implement using context.{{feature}} (Domain.Entities.{{entity}}) and context.SaveChangesAsync(cancellationToken)
                    throw new NotImplementedException();
                }
            }

            """),
            ($"{name}CommandValidator.cs", $$"""
            using FluentValidation;

            namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{name}};

            public class {{name}}CommandValidator : AbstractValidator<{{name}}Command>
            {
                public {{name}}CommandValidator()
                {
                    // TODO: add validation rules, e.g. RuleFor(v => v.Title).NotEmpty();
                }
            }

            """)
        };
}
