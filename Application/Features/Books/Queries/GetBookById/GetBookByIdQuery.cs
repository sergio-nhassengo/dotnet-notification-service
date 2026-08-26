using Application.Features.Books.Queries.GetBooks;
using Application.Common.Interfaces;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Books.Queries.GetBookById;

public record GetBookByIdQuery(int Id) : IRequest<BookDto>;

public class GetBookByIdQueryHandler(IApplicationDbContext context, IConfigurationProvider mapperConfiguration)
    : IRequestHandler<GetBookByIdQuery, BookDto>
{
    public async Task<BookDto> Handle(GetBookByIdQuery request, CancellationToken cancellationToken)
    {
        var result = await context.Books
            .Where(x => x.Id == request.Id)
            .ProjectTo<BookDto>(mapperConfiguration)
            .FirstOrDefaultAsync(cancellationToken);

        return result ?? throw new NotFoundException(nameof(Domain.Entities.Book), request.Id);
    }
}
