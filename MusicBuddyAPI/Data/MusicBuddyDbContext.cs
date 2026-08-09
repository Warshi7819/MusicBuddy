using Microsoft.EntityFrameworkCore;
using MusicBuddyShared.Models;

namespace MusicBuddyAPI.Data;

public class MusicBuddyDbContext : DbContext
{
    public MusicBuddyDbContext(DbContextOptions<MusicBuddyDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Theme> Themes => Set<Theme>();
}
