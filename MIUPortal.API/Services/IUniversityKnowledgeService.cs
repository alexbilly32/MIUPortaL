using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MIUPortal.API.Data;
using MIUPortal.API.Models;

namespace MIUPortal.API.Services
{
    public interface IUniversityKnowledgeService
    {
        Task<string> BuildContextAsync(string userMessage);
    }

    /// <summary>
    /// Pulls relevant, non-sensitive facts from two places:
    ///   1. Live portal data (programmes, fee structures, faculties, campuses)
    ///   2. The knowledge_articles table (curated FAQs + synced website content)
    /// and assembles them into a short context block for the LLM prompt.
    ///
    /// This is deliberately keyword-based rather than embeddings/vector search —
    /// no extra infra needed, works offline for a defense demo, and is easy to
    /// reason about. Swap for a vector store later if you want semantic search.
    /// </summary>
    public class UniversityKnowledgeService : IUniversityKnowledgeService
    {
        private readonly MIUContext _db;

        public UniversityKnowledgeService(MIUContext db)
        {
            _db = db;
        }

        public async Task<string> BuildContextAsync(string userMessage)
        {
            var keywords = ExtractKeywords(userMessage);
            var sb = new StringBuilder();

            // ---- 1. Curated / synced knowledge base ----
            var articles = await _db.KnowledgeArticles
                .Where(a => a.IsActive)
                .ToListAsync();

            var scored = articles
                .Select(a => new
                {
                    Article = a,
                    Score = (ScoreMatch(a.Tags, keywords) * 3
                           + ScoreMatch(a.Category, keywords) * 2
                           + ScoreMatch(a.Title, keywords) * 2
                           + ScoreMatch(a.Content, keywords))
                           * Math.Max(a.Priority, 1)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(4)
                .ToList();

            foreach (var item in scored)
            {
                sb.AppendLine($"- {item.Article.Title}: {Truncate(item.Article.Content, 400)}");
            }

            // ---- 2. Live portal facts (fees, faculty/school assignment, duration) ----
            var programmeTriggers = new[]
            {
                "fee", "fees", "tuition", "programme", "program", "course", "cost", "price",
                "faculty", "faculties", "school", "department", "belong", "belongs", "under",
                "leader", "head", "heads", "duration", "years", "offered"
            };

            if (keywords.Overlaps(programmeTriggers))
            {
                var programmes = await _db.Programmes
                    .Include(p => p.School)
                        .ThenInclude(s => s.Faculty)
                    .Where(p => p.IsActive == true)
                    .Select(p => new
                    {
                        p.ProgrammeCode,
                        p.ProgrammeName,
                        p.TuitionFeePerSemester,
                        p.DurationYears,
                        p.ProgrammeLeader,
                        p.Department,
                        SchoolName = p.School != null ? p.School.SchoolName : null,
                        FacultyName = p.School != null && p.School.Faculty != null ? p.School.Faculty.FacultyName : null,
                        FacultyHead = p.School != null && p.School.Faculty != null ? p.School.Faculty.HeadOfDepartment : null
                    })
                    .Take(30)
                    .ToListAsync();

                if (programmes.Any())
                {
                    sb.AppendLine("- Programme records (from live university data):");
                    foreach (var p in programmes)
                    {
                        var faculty = p.FacultyName ?? p.Department ?? "not on record";
                        var school = p.SchoolName != null ? $", School: {p.SchoolName}" : "";
                        var head = !string.IsNullOrWhiteSpace(p.FacultyHead) ? $", Head of Faculty: {p.FacultyHead}" : "";
                        var leader = !string.IsNullOrWhiteSpace(p.ProgrammeLeader) ? $", Programme Leader: {p.ProgrammeLeader}" : "";
                        sb.AppendLine($"  * {p.ProgrammeName} ({p.ProgrammeCode}): Faculty: {faculty}{head}{school}, Duration: {p.DurationYears} years, Tuition/semester: {p.TuitionFeePerSemester:N0} UGX{leader}");
                    }
                }
            }

            return sb.Length == 0
                ? "(No specific matching records found — answer generally from known MIU public information, or say you don't have that detail.)"
                : sb.ToString();
        }

        private static HashSet<string> ExtractKeywords(string text)
        {
            var stopWords = new HashSet<string> { "the", "is", "at", "on", "in", "a", "an", "to", "of", "for", "do", "does", "what", "how", "i", "my", "can", "you" };
            return text
                .ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .ToHashSet();
        }

        private static int ScoreMatch(string haystack, HashSet<string> keywords)
        {
            var haystackLower = haystack.ToLowerInvariant();
            return keywords.Count(k => haystackLower.Contains(k));
        }

        private static string Truncate(string text, int max) =>
            text.Length <= max ? text : text.Substring(0, max) + "...";
    }
}