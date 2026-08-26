using Domain.Common;

namespace Domain.Entities;

public class TodoItem : BaseAuditableEntity<int>
{
    public int TodoListId { get; set; }

    public TodoList? TodoList { get; set; }

    public string Title { get; set; } = string.Empty;

    public bool Done { get; set; }
}
