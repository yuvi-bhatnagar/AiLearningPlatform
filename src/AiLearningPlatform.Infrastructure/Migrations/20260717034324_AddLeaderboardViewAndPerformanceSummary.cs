using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiLearningPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardViewAndPerformanceSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptDateUtc",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Streak",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Create LeaderboardView
            migrationBuilder.Sql(@"
                EXEC('CREATE VIEW LeaderboardView AS
                SELECT 
                    u.Id AS UserId,
                    u.Username,
                    COALESCE(SUM(a.Score), 0) AS TotalScore,
                    COUNT(a.Id) AS QuizzesAttempted,
                    CAST(DENSE_RANK() OVER (ORDER BY COALESCE(SUM(a.Score), 0) DESC) AS INT) AS [Rank]
                FROM Users u
                LEFT JOIN Attempts a ON u.Id = a.UserId AND a.Status = ''Graded''
                GROUP BY u.Id, u.Username')
            ");

            // Create GetStudentPerformanceSummary stored procedure
            migrationBuilder.Sql(@"
                EXEC('CREATE PROCEDURE GetStudentPerformanceSummary
                    @UserId UNIQUEIDENTIFIER
                AS
                BEGIN
                    SELECT 
                        u.Id AS UserId,
                        u.Username,
                        COALESCE(SUM(a.Score), 0) AS TotalScore,
                        COALESCE(AVG(a.Score), 0) AS AverageScore,
                        COUNT(a.Id) AS TotalAttempts,
                        COALESCE(MAX(a.Score), 0) AS HighestScore,
                        COALESCE(MIN(a.Score), 0) AS LowestScore
                    FROM Users u
                    LEFT JOIN Attempts a ON u.Id = a.UserId AND a.Status = ''Graded''
                    WHERE u.Id = @UserId
                    GROUP BY u.Id, u.Username
                END')
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS GetStudentPerformanceSummary;");
            migrationBuilder.Sql("DROP VIEW IF EXISTS LeaderboardView;");

            migrationBuilder.DropColumn(
                name: "LastAttemptDateUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Streak",
                table: "Users");
        }
    }
}
