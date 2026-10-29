namespace Aw.Context.Testing.Fixtures;

/// <summary>
/// The files of a small TypeScript app on Drizzle for the data-store pass: a schema file declaring three tables (two dialects, the SQL name differing from the exported name),
/// a barrel that re-exports it and creates the client, routes that read and write the tables through an alias import, a renamed import, a namespace import and a relational
/// query, a file that only mentions a table, and two zod schema files that declare no table.
/// </summary>
public static class DrizzleRepo
{
    /// <summary>The repo's name in the code map.</summary>
    public const string Repo = "aw-notes";

    /// <summary>Path to text, in the order a listing would give them.</summary>
    public static IReadOnlyDictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["tsconfig.json"] = "{ \"compilerOptions\": { \"baseUrl\": \".\", \"paths\": { \"@/*\": [\"src/*\"] } } }\n",
        ["src/db/schema.ts"] = "import { pgTable, serial, text, integer } from 'drizzle-orm/pg-core';\nimport { sqliteTable } from 'drizzle-orm/sqlite-core';\n\nexport const posts = pgTable('blog_posts', { id: serial('id').primaryKey(), title: text('title') });\nexport const authors = pgTable('authors', { id: serial('id').primaryKey(), name: text('name') });\nexport const tags = sqliteTable('post_tags', { id: integer('id').primaryKey(), label: text('label') });\n",
        ["src/db/index.ts"] = "import { drizzle } from 'drizzle-orm/node-postgres';\nimport * as schema from './schema';\n\nexport * from './schema';\nexport const db = drizzle(client, { schema });\n",
        ["src/routes/list-posts.ts"] = "import { db, posts, authors } from '@/db';\n\nexport const listPosts = () => db.select().from(posts).leftJoin(authors, eq(posts.authorId, authors.id));\n",
        ["src/routes/create-post.ts"] = "import { db } from '@/db';\nimport { posts as p } from '../db/schema';\n\nexport const createPost = (title: string) => db.insert(p).values({ title });\n",
        ["src/routes/tag.ts"] = "import * as schema from '@/db/schema';\nimport { db } from '@/db';\n\nexport const dropTags = () => db.delete(schema.tags);\nexport const recent = () => db.query.posts.findMany({ limit: 10 });\n",
        ["src/routes/notes.ts"] = "// posts and authors are read in list-posts.ts\nexport const done = () => logger.info('cleaned up blog_posts rows');\n",
        ["src/forms/schema.ts"] = "import { z } from 'zod';\n\nexport const postForm = z.object({ title: z.string() });\n",
        ["src/lib/zod-schema.ts"] = "import { z } from 'zod';\n\nexport const tagForm = z.object({ label: z.string() });\n",
    };
}
