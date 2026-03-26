using Microsoft.EntityFrameworkCore;
using KEYREGISTERAUTOMATION.Models;

namespace KEYREGISTERAUTOMATION.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public DbSet<UserAccount> UserAccounts { get; set; }
        public DbSet<AllKeyTags> KeyTags { get; set; }
        public DbSet<OfficeInfo> OfficeInfos { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<AppUser> AppUsers { get; set; }
        public DbSet<VwStaff> vwstaff { get; set; }
        public DbSet<AssignmentRecord> AssignmentRecords { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<VwStaff>(entity =>
            {
                entity.HasKey(e => e.IGG);
                entity.ToView("VwStaff");
            });
        }

    }
}