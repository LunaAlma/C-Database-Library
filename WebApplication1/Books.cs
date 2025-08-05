using System;
using System.ComponentModel.DataAnnotations;

namespace BookApi.Models
{
    public class Book
    {
        public int id { get; set; }

        [Required]
        public string? title { get; set; }  // we'll enforce uniqueness in the DbContext

        [Required]
        public string author { get; set; }

        public DateTime release_date { get; set; }
    }
}