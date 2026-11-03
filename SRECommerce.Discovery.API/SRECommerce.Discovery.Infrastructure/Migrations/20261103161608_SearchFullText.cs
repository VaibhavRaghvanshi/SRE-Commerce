using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SRECommerce.Discovery.Infrastructure.Migrations;

[DbContext(typeof(DiscoveryDbContext))]
[Migration("20261103161608_SearchFullText")]
public sealed class SearchFullText : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') <> 1
                THROW 51000, 'SQL Server Full-Text Search is required for Discovery.', 1;
            IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name=N'DiscoveryFullText')
                EXEC(N'CREATE FULLTEXT CATALOG DiscoveryFullText');
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'dbo.SearchDocuments'))
                EXEC(N'CREATE FULLTEXT INDEX ON dbo.SearchDocuments(SearchText LANGUAGE 0)
                    KEY INDEX PK_SearchDocuments ON DiscoveryFullText WITH (CHANGE_TRACKING=AUTO, STOPLIST=OFF)');
            """, suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP FULLTEXT INDEX ON dbo.SearchDocuments", suppressTransaction: true);
        migrationBuilder.Sql("DROP FULLTEXT CATALOG DiscoveryFullText", suppressTransaction: true);
    }
}
