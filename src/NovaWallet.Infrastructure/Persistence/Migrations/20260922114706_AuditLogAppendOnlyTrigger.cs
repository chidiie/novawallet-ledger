using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaWallet.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditLogAppendOnlyTrigger : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
        CREATE OR REPLACE FUNCTION audit_logs_reject_mutation() RETURNS trigger AS $$
        BEGIN
            RAISE EXCEPTION 'audit_logs is append-only: % is not permitted', TG_OP;
        END;
        $$ LANGUAGE plpgsql;

        CREATE TRIGGER audit_logs_no_update_or_delete
            BEFORE UPDATE OR DELETE ON audit_logs
            FOR EACH ROW EXECUTE FUNCTION audit_logs_reject_mutation();

        CREATE TRIGGER audit_logs_no_truncate
            BEFORE TRUNCATE ON audit_logs
            FOR EACH STATEMENT EXECUTE FUNCTION audit_logs_reject_mutation();
        """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
        DROP TRIGGER IF EXISTS audit_logs_no_truncate ON audit_logs;
        DROP TRIGGER IF EXISTS audit_logs_no_update_or_delete ON audit_logs;
        DROP FUNCTION IF EXISTS audit_logs_reject_mutation();
        """);
        }
    }
}
