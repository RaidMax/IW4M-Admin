using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations.Postgresql
{
    public partial class AddDescendingTimeSentIndexEFClientMessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"CREATE INDEX""IX_EFClientMessages_TimeSentDesc""
                ON public.""EFClientMessages"" USING btree
                (""TimeSent"" DESC NULLS LAST)
                TABLESPACE pg_default;"
            );
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX public.""IX_EFClientMessages_TimeSentDesc""");
        }
    }
}
