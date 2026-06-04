using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CTS_backend.Models;

public class BctqQuestion
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("content")]
    public String Content { get; set; } = string.Empty;
    [Column("question_type_id")]
    public int QuestionTypeId { get; set; }
    public QuestionType? Name { get; set; }
    public ICollection<BctqAnswer>? Answers { get; set; }
}