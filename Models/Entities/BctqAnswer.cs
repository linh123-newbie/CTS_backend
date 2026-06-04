using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CTS_backend.Models;

public class BctqAnswer
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("bctq_question_id")]
    public int BctqQuestionId { get; set; }
    [Column("answer_content")]
    public String? AnswerContent { get; set; }
    [Column("rate")]
    public int Rate { get; set; }
    public BctqQuestion? BctqQuestion { get; set; }
}