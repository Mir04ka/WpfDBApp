using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WpfDBApp.Models;

[Table("Persons")]
public class Person
{
    [Key]
    public int Id { get; set; }
    
    public DateTime Date { get; set; }
    
    [MaxLength(50)]
    public required string FirstName { get; set; }
    
    [MaxLength(50)]
    public required string LastName { get; set; }

    [MaxLength(50)]
    public required string SurName { get; set; }

    [MaxLength(50)]
    public required string City { get; set; }

    [MaxLength(50)]
    public required string Country { get; set; }

}