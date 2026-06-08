using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MovieApp.Migrations
{
    /// <inheritdoc />
    public partial class SupportChatUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportTickets_Users_UserId",
                table: "SupportTickets");

            migrationBuilder.RenameColumn(
                name: "Reason",
                table: "SupportTickets",
                newName: "Subject");

            migrationBuilder.CreateTable(
                name: "SupportMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TicketId = table.Column<int>(type: "integer", nullable: false),
                    SenderId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportMessages_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupportMessages_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Migrate data before dropping old columns
            migrationBuilder.Sql("INSERT INTO \"SupportMessages\" (\"TicketId\", \"SenderId\", \"Text\", \"CreatedAt\") SELECT \"Id\", \"UserId\", \"Message\", \"CreatedAt\" FROM \"SupportTickets\" WHERE \"Message\" IS NOT NULL AND \"Message\" <> ''");
            migrationBuilder.Sql("INSERT INTO \"SupportMessages\" (\"TicketId\", \"SenderId\", \"Text\", \"CreatedAt\") SELECT t.\"Id\", COALESCE((SELECT u.\"Id\" FROM \"Users\" u WHERE LOWER(u.\"Role\") = 'admin' ORDER BY u.\"Id\" LIMIT 1), t.\"UserId\"), t.\"AdminReply\", t.\"CreatedAt\" FROM \"SupportTickets\" t WHERE t.\"AdminReply\" IS NOT NULL AND t.\"AdminReply\" <> ''");

            migrationBuilder.DropColumn(
                name: "AdminReply",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "SupportTickets");

            migrationBuilder.CreateIndex(
                name: "IX_SupportMessages_SenderId",
                table: "SupportMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportMessages_TicketId",
                table: "SupportMessages",
                column: "TicketId");

            migrationBuilder.AddForeignKey(
                name: "FK_SupportTickets_Users_UserId",
                table: "SupportTickets",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SupportTickets_Users_UserId",
                table: "SupportTickets");

            migrationBuilder.DropTable(
                name: "SupportMessages");

            migrationBuilder.RenameColumn(
                name: "Subject",
                table: "SupportTickets",
                newName: "Reason");

            migrationBuilder.AddColumn<string>(
                name: "AdminReply",
                table: "SupportTickets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "SupportTickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddForeignKey(
                name: "FK_SupportTickets_Users_UserId",
                table: "SupportTickets",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
