using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopPulse.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Audio" },
                    { 2, "Clavier" },
                    { 3, "Écran" }
                });

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill depuis l'ancienne colonne texte — robuste même pour des lignes
            // hors seed (l'UpdateData ×200 généré par HasData ne couvrirait que le seed).
            migrationBuilder.Sql("""
                UPDATE products SET "CategoryId" = categories."Id"
                FROM categories
                WHERE categories."Name" = products."Category";
                """);

            migrationBuilder.DropIndex(
                name: "IX_products_Category",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "products");

            migrationBuilder.CreateIndex(
                name: "IX_products_CategoryId",
                table: "products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_categories_Name",
                table: "categories",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_products_categories_CategoryId",
                table: "products",
                column: "CategoryId",
                principalTable: "categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_categories_CategoryId",
                table: "products");

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE products SET "Category" = categories."Name"
                FROM categories
                WHERE categories."Id" = products."CategoryId";
                """);

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropIndex(
                name: "IX_products_CategoryId",
                table: "products");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "products");

            migrationBuilder.CreateIndex(
                name: "IX_products_Category",
                table: "products",
                column: "Category");
        }
    }
}
