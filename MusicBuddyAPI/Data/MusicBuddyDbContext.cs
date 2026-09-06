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
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<ArtistArtPreference> ArtistArtPreferences => Set<ArtistArtPreference>();
    public DbSet<TrackMetadata> TrackMetadata => Set<TrackMetadata>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlaylistTrack>()
            .HasOne<Playlist>()
            .WithMany()
            .HasForeignKey(t => t.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Playlist>()
            .HasIndex(p => new { p.UserId, p.FileType });

        modelBuilder.Entity<ArtistArtPreference>()
            .HasIndex(a => new { a.UserId, a.ArtistPath })
            .IsUnique();

        modelBuilder.Entity<TrackMetadata>()
            .HasIndex(t => t.Artist);
        modelBuilder.Entity<TrackMetadata>()
            .HasIndex(t => t.Genre);
        modelBuilder.Entity<TrackMetadata>()
            .HasIndex(t => t.TrackName);
        modelBuilder.Entity<TrackMetadata>()
            .HasIndex(t => t.FilePath)
            .IsUnique();
    }
}
