namespace Aw.Sindri.Infrastructure.Memory;

/// <summary>The memory SQL (ledger.db, migration 0011_memory). A memory's id is its name and a fact's id is its key.</summary>
internal static class MemorySql
{
    internal const string MemoryColumns = "name, description, created_by, created_at";

    internal const string FactColumns = "id, memory_id, text, embedding, tags, workflow_id, run_id, item_id, session_id, expires_at, created_at, refreshed_at, model, dimension, written_by, refreshed_by";

    internal const string InsertMemory = "INSERT INTO memory (id, name, description, created_by, created_at) VALUES ($name, $name, $description, $createdBy, $createdAt)";

    internal const string MemoryExists = "SELECT EXISTS (SELECT 1 FROM memory WHERE id = $name)";

    internal const string SelectMemory = "SELECT " + MemoryColumns + " FROM memory WHERE id = $name";

    internal const string SelectMemories = "SELECT " + MemoryColumns + " FROM memory ORDER BY name";

    internal const string SelectCounts = "SELECT memory_id, COUNT(*) FROM memory_fact GROUP BY memory_id";

    internal const string SelectFacts = "SELECT " + FactColumns + " FROM memory_fact WHERE memory_id = $name ORDER BY created_at, rowid";

    internal const string InsertFact =
        "INSERT INTO memory_fact (id, memory_id, text, embedding, tags, workflow_id, run_id, item_id, session_id, expires_at, created_at, refreshed_at, model, dimension, written_by, refreshed_by) " +
        "VALUES ($key, $name, $text, $embedding, $tags, $workflow, $run, $item, $session, $expiresAt, $createdAt, $refreshedAt, $model, $dimension, $writtenBy, $refreshedBy)";

    internal const string Refresh =
        "UPDATE memory_fact SET refreshed_at = $refreshedAt, expires_at = COALESCE($expiresAt, expires_at), refreshed_by = COALESCE($refreshedBy, refreshed_by) WHERE memory_id = $name AND id = $key";

    internal const string Delete =
        "DELETE FROM memory_fact WHERE memory_id = $name AND (" +
        "($key IS NOT NULL AND id = $key) " +
        "OR ($tag IS NOT NULL AND EXISTS (SELECT 1 FROM json_each(memory_fact.tags) WHERE value = $tag)) " +
        "OR ($expiredAt IS NOT NULL AND expires_at IS NOT NULL AND expires_at <= $expiredAt))";

    internal const string SelectUnembedded = "SELECT " + FactColumns + " FROM memory_fact WHERE embedding IS NULL ORDER BY created_at, rowid LIMIT $limit";

    internal const string SetEmbedding =
        "UPDATE memory_fact SET embedding = $embedding, model = $model, dimension = $dimension WHERE memory_id = $name AND id = $key AND embedding IS NULL";

    internal const string DeleteExpired = "DELETE FROM memory_fact WHERE expires_at IS NOT NULL AND expires_at <= $now";
}
