namespace UniversiteSss.Models;

public class DbModel
{
    public List<User> Users { get; set; } = [];
    public List<Faq> Faqs { get; set; } = [];
    public List<QuestionRequest> QuestionRequests { get; set; } = [];
}

public class User
{
    public string Id { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "student";
    public DateTime CreatedAt { get; set; }
}

public class Faq
{
    public string Id { get; set; } = "";
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string Topic { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<FaqHistory> History { get; set; } = [];
}

public class FaqHistory
{
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string Topic { get; set; } = "";
    public string UpdatedBy { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

public class QuestionRequest
{
    public string Id { get; set; } = "";
    public string Question { get; set; } = "";
    public string Details { get; set; } = "";
    public string Topic { get; set; } = "";
    public string RequestedBy { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string ReviewNote { get; set; } = "";
    public string? ModeratedBy { get; set; }
    public string? PublishedFaqId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<RequestHistory> History { get; set; } = [];
}

public class RequestHistory
{
    public string Action { get; set; } = "";
    public string By { get; set; } = "";
    public DateTime At { get; set; }
    public string Note { get; set; } = "";
}
