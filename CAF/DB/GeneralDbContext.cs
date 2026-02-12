namespace CAF.DB;

public class GeneralDbContext(DbContextOptions<GeneralDbContext> options) : DbContext(options)
{
    public DbSet<LLMRequestLogEntity> LLMRequestLogs { get; set; }
    public DbSet<SystemMessage> SystemMessages { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<Turn> Turns { get; set; }
    public DbSet<Flag> Flags { get; set; }
    public DbSet<Setting> Settings { get; set; }
    public DbSet<Profile> Profiles { get; set; }
    public DbSet<ContextData> ContextData { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ContextData (new unified entity)
        modelBuilder.Entity<ContextData>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Indexes for common queries
            entity.HasIndex(e => e.ProfileId);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Availability);
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.IsArchived);
            entity.HasIndex(e => e.IsUser);
            entity.HasIndex(e => new { e.ProfileId, e.Type, e.Availability });
            entity.HasIndex(e => new { e.ProfileId, e.Availability, e.IsEnabled, e.IsArchived });
            entity.HasIndex(e => new { e.ProfileId, e.Type, e.IsUser }); // For user profile lookup
            entity.HasIndex(e => e.VectorId);
            entity.HasIndex(e => e.InVectorDb);
            entity.HasIndex(e => e.SourceSessionId);
            entity.HasIndex(e => e.Speaker);
            entity.HasIndex(e => e.RelevanceScore);
            entity.HasIndex(e => e.UsedLastOnTurnId);
            entity.HasIndex(e => new { e.ProfileId, e.Type, e.InVectorDb }); // For embedding queries
            entity.HasIndex(e => new { e.ProfileId, e.Type, e.SourceSessionId }); // For session-based queries

            // Configure relationship with Profile
            entity.HasOne(e => e.Profile)
                .WithMany()
                .HasForeignKey(e => e.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Tags as JSON
            entity.Property(e => e.Tags)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());
        });

        // Configure LLMRequestLogEntity
        modelBuilder.Entity<LLMRequestLogEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Indexes for common queries
            entity.HasIndex(e => e.RequestId)
                .IsUnique();

            entity.HasIndex(e => e.Provider);

            entity.HasIndex(e => e.Model);

            entity.HasIndex(e => e.StartTime);

            entity.HasIndex(e => new { e.Provider, e.StartTime });

            entity.HasIndex(e => new { e.Provider, e.Model, e.StartTime });

            entity.HasIndex(e => e.TotalCost);

            entity.HasIndex(e => e.DurationMs);

            // Indexes for cache and thinking tokens
            entity.HasIndex(e => e.CachedContentTokenCount);

            entity.HasIndex(e => e.ThinkingTokens);

            // Index for TurnId
            entity.HasIndex(e => e.TurnId);

            entity.Property(e => e.TotalCost)
                .HasPrecision(18, 6);

            entity.Property(e => e.RequestId)
                .HasMaxLength(50);

            entity.Property(e => e.Provider)
                .HasMaxLength(50);

            entity.Property(e => e.Model)
                .HasMaxLength(100);

            entity.Property(e => e.Operation)
                .HasMaxLength(100);

            entity.Property(e => e.Currency)
                .HasMaxLength(10);

            // Configure foreign key relationship
            entity.HasOne(e => e.Turn)
                .WithMany()
                .HasForeignKey(e => e.TurnId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure SystemMessage
        modelBuilder.Entity<SystemMessage>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Indexes for common queries
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.IsArchived);
            entity.HasIndex(e => e.ParentId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new { e.Type, e.IsActive });
            entity.HasIndex(e => new { e.Type, e.IsActive, e.IsArchived });
        });

        // Configure Session
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Number).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasMany(e => e.Turns)
                .WithOne(e => e.Session)
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Turn
        modelBuilder.Entity<Turn>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.SessionId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Accepted);
            entity.HasIndex(e => new { e.SessionId, e.CreatedAt });
        });

        // Configure Flag
        modelBuilder.Entity<Flag>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Value);
            entity.HasIndex(e => e.Active);
            entity.HasIndex(e => e.Constant);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure Setting
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}