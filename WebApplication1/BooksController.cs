using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookApi.Data;
using BookApi.Models;

namespace BookApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BooksController : ControllerBase
    {
        private readonly BooksContext _context;
        public BooksController(BooksContext context) => _context = context;

        // api/books deliricem
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Book>>> GetBooks() =>
            await _context.books.OrderBy(b => b.title).ToListAsync();

        // api/books/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Book>> GetBook(int id)
        {
            var book = await _context.books.FindAsync(id);
            return book == null ? NotFound() : book;
        }

        // api/books
        [HttpPost]
        public async Task<ActionResult<Book>> PostBook([FromBody] Book book)
        {
            _context.books.Add(book);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (await _context.books.AnyAsync(b => b.title == book.title))
                    return Conflict($"A book with title '{book.title}' already exists.");
                throw;
            }

            // returns 201 
            return CreatedAtAction(nameof(GetBook), new { id = book.id }, book);
        }

        // DELETE
        [HttpDelete("{title}")]
        public async Task<IActionResult> DeleteByTitle(string title)
        {
            var book = await _context.books
                                     .FirstOrDefaultAsync(b => b.title == title);
            if (book == null)
                return NotFound($"No book found with title '{title}'.");

            _context.books.Remove(book);
            await _context.SaveChangesAsync();
            return Ok($"Removed book titled '{title}'.");
        }
    }
}
