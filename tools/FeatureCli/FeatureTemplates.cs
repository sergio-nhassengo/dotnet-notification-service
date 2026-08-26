namespace FeatureCli;

internal sealed record PropertySpec(string Name, string Type)
{
    public bool IsNonNullableString => Type == "string";
}

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

    public static (string Category, string Name, (string FileName, string Content)[] Files)[] Crud(
        string entity, string plural, string feature, string rootNamespace, string dbContextNamespace,
        string dbContextType, string keyType, PropertySpec[] properties)
    {
        var createName = $"Create{entity}";
        var updateName = $"Update{entity}";
        var deleteName = $"Delete{entity}";
        var getByIdName = $"Get{entity}ById";
        var getAllName = $"Get{plural}";

        var createParams = string.Join(", ", properties.Select(p => $"{p.Type} {p.Name}"));
        var updateParams = properties.Length == 0
            ? $"{keyType} Id"
            : $"{keyType} Id, {string.Join(", ", properties.Select(p => $"{p.Type} {p.Name}"))}";

        var createObjectInit = properties.Length == 0
            ? $"new {entity}()"
            : $"new {entity}\n        {{\n" +
              string.Join(",\n", properties.Select(p => $"            {p.Name} = request.{p.Name}")) +
              "\n        }";

        var updateAssignments = properties.Length == 0
            ? "// TODO: nothing to update yet - add properties with --properties"
            : string.Join("\n        ", properties.Select(p => $"entity.{p.Name} = request.{p.Name};"));

        var createRules = BuildCreateRules(properties);
        var updateRules = BuildUpdateRules(keyType, properties);

        var orderBy = properties.Length > 0
            ? $"\n            .OrderBy(x => x.{properties[0].Name})"
            : "\n            .OrderBy(x => x.Id)";

        var dtoProps = properties.Length == 0
            ? ""
            : "\n" + string.Join("\n\n", properties.Select(p =>
                $"    public {p.Type} {p.Name} {{ get; init; }}{(p.IsNonNullableString ? " = string.Empty;" : "")}"));

        return
        [
            ("Commands", createName,
            [
                ($"{createName}Command.cs", $$"""
                using {{dbContextNamespace}};
                using Domain.Entities;
                using MediatR;

                namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{createName}};

                public record {{createName}}Command({{createParams}}) : IRequest<{{keyType}}>;

                public class {{createName}}CommandHandler({{dbContextType}} context) : IRequestHandler<{{createName}}Command, {{keyType}}>
                {
                    public async Task<{{keyType}}> Handle({{createName}}Command request, CancellationToken cancellationToken)
                    {
                        var entity = {{createObjectInit}};

                        context.{{plural}}.Add(entity);

                        await context.SaveChangesAsync(cancellationToken);

                        return entity.Id;
                    }
                }

                """),
                ($"{createName}CommandValidator.cs", $$"""
                using FluentValidation;

                namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{createName}};

                public class {{createName}}CommandValidator : AbstractValidator<{{createName}}Command>
                {
                    public {{createName}}CommandValidator()
                    {
                        {{createRules}}
                    }
                }

                """)
            ]),
            ("Commands", updateName,
            [
                ($"{updateName}Command.cs", $$"""
                using {{dbContextNamespace}};
                using Domain.Exceptions;
                using MediatR;
                using Microsoft.EntityFrameworkCore;

                namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{updateName}};

                public record {{updateName}}Command({{updateParams}}) : IRequest;

                public class {{updateName}}CommandHandler({{dbContextType}} context) : IRequestHandler<{{updateName}}Command>
                {
                    public async Task Handle({{updateName}}Command request, CancellationToken cancellationToken)
                    {
                        var entity = await context.{{plural}}
                            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

                        if (entity is null)
                        {
                            throw new NotFoundException(nameof(Domain.Entities.{{entity}}), request.Id);
                        }

                        {{updateAssignments}}

                        await context.SaveChangesAsync(cancellationToken);
                    }
                }

                """),
                ($"{updateName}CommandValidator.cs", $$"""
                using FluentValidation;

                namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{updateName}};

                public class {{updateName}}CommandValidator : AbstractValidator<{{updateName}}Command>
                {
                    public {{updateName}}CommandValidator()
                    {
                        {{updateRules}}
                    }
                }

                """)
            ]),
            ("Commands", deleteName,
            [
                ($"{deleteName}Command.cs", $$"""
                using {{dbContextNamespace}};
                using Domain.Exceptions;
                using MediatR;
                using Microsoft.EntityFrameworkCore;

                namespace {{rootNamespace}}.Features.{{feature}}.Commands.{{deleteName}};

                public record {{deleteName}}Command({{keyType}} Id) : IRequest;

                public class {{deleteName}}CommandHandler({{dbContextType}} context) : IRequestHandler<{{deleteName}}Command>
                {
                    public async Task Handle({{deleteName}}Command request, CancellationToken cancellationToken)
                    {
                        var entity = await context.{{plural}}
                            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

                        if (entity is null)
                        {
                            throw new NotFoundException(nameof(Domain.Entities.{{entity}}), request.Id);
                        }

                        context.{{plural}}.Remove(entity);

                        await context.SaveChangesAsync(cancellationToken);
                    }
                }

                """)
            ]),
            ("Queries", getByIdName,
            [
                ($"{getByIdName}Query.cs", $$"""
                using {{rootNamespace}}.Features.{{feature}}.Queries.{{getAllName}};
                using {{dbContextNamespace}};
                using AutoMapper;
                using AutoMapper.QueryableExtensions;
                using Domain.Exceptions;
                using MediatR;
                using Microsoft.EntityFrameworkCore;

                namespace {{rootNamespace}}.Features.{{feature}}.Queries.{{getByIdName}};

                public record {{getByIdName}}Query({{keyType}} Id) : IRequest<{{entity}}Dto>;

                public class {{getByIdName}}QueryHandler({{dbContextType}} context, IConfigurationProvider mapperConfiguration)
                    : IRequestHandler<{{getByIdName}}Query, {{entity}}Dto>
                {
                    public async Task<{{entity}}Dto> Handle({{getByIdName}}Query request, CancellationToken cancellationToken)
                    {
                        var result = await context.{{plural}}
                            .Where(x => x.Id == request.Id)
                            .ProjectTo<{{entity}}Dto>(mapperConfiguration)
                            .FirstOrDefaultAsync(cancellationToken);

                        return result ?? throw new NotFoundException(nameof(Domain.Entities.{{entity}}), request.Id);
                    }
                }

                """)
            ]),
            ("Queries", getAllName,
            [
                ($"{getAllName}Query.cs", $$"""
                using MediatR;

                namespace {{rootNamespace}}.Features.{{feature}}.Queries.{{getAllName}};

                public record {{getAllName}}Query : IRequest<List<{{entity}}Dto>>;

                """),
                ($"{getAllName}QueryHandler.cs", $$"""
                using {{dbContextNamespace}};
                using AutoMapper;
                using AutoMapper.QueryableExtensions;
                using MediatR;
                using Microsoft.EntityFrameworkCore;

                namespace {{rootNamespace}}.Features.{{feature}}.Queries.{{getAllName}};

                public class {{getAllName}}QueryHandler({{dbContextType}} context, IConfigurationProvider mapperConfiguration)
                    : IRequestHandler<{{getAllName}}Query, List<{{entity}}Dto>>
                {
                    public Task<List<{{entity}}Dto>> Handle({{getAllName}}Query request, CancellationToken cancellationToken)
                    {
                        return context.{{plural}}{{orderBy}}
                            .ProjectTo<{{entity}}Dto>(mapperConfiguration)
                            .ToListAsync(cancellationToken);
                    }
                }

                """),
                ($"{entity}Dto.cs", $$"""
                using AutoMapper;

                namespace {{rootNamespace}}.Features.{{feature}}.Queries.{{getAllName}};

                public class {{entity}}Dto
                {
                    public {{keyType}} Id { get; init; }
                {{dtoProps}}

                    private class Mapping : Profile
                    {
                        public Mapping()
                        {
                            CreateMap<Domain.Entities.{{entity}}, {{entity}}Dto>();
                        }
                    }
                }

                """)
            ])
        ];
    }

    private static string BuildCreateRules(PropertySpec[] properties)
    {
        var stringProps = properties.Where(p => p.IsNonNullableString).ToArray();

        if (stringProps.Length == 0)
        {
            return "// TODO: add validation rules, e.g. RuleFor(v => v.Title).NotEmpty();";
        }

        return string.Join("\n\n        ", stringProps.Select(p => $$"""
        RuleFor(v => v.{{p.Name}})
                    .NotEmpty()
                    .MaximumLength(200);
        """.Trim()));
    }

    private static string BuildUpdateRules(string keyType, PropertySpec[] properties)
    {
        var rules = new System.Collections.Generic.List<string>();

        if (keyType == "int")
        {
            rules.Add("RuleFor(v => v.Id)\n                    .GreaterThan(0);");
        }

        rules.AddRange(properties.Where(p => p.IsNonNullableString)
            .Select(p => $"RuleFor(v => v.{p.Name})\n                    .NotEmpty()\n                    .MaximumLength(200);"));

        return rules.Count == 0
            ? "// TODO: add validation rules"
            : string.Join("\n\n        ", rules);
    }
}
