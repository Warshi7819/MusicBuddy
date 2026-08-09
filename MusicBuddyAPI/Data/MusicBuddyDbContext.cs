using Microsoft.EntityFrameworkCore;
using MusicBuddyShared.Models;

namespace MusicBuddyAPI.Data;

public class MusicBuddyDbContext : DbContext
{
    public MusicBuddyDbContext(DbContextOptions<MusicBuddyDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Theme> Themes => Set<Theme>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<PlaylistTrack> PlaylistTracks => Set<PlaylistTrack>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlaylistTrack>()
            .HasOne<Playlist>()
            .WithMany()
            .HasForeignKey(t => t.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Playlist>()
            .HasIndex(p => new { p.UserId, p.FileType });
    }
}
