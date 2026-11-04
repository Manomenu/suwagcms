import type { ReactNode } from "react";
import { Link } from "wouter";

import { Button } from "@/design/button";
import { Input } from "@/design/input";

import type { Article, Filters, KnowledgeBase } from "./api";

const SELECT = "h-9 rounded-md border border-input bg-background px-2 text-sm";

/** The Articles screen: the heading, the filters, the list with its actions, and whatever form is open. */
export function ArticlesView({
    bot,
    articles,
    bases,
    filters,
    form,
    onFilters,
    onAdd,
    onEdit,
    onDelete,
    onLeave,
}: {
    bot: string;
    articles: Article[];
    bases: KnowledgeBase[];
    filters: Filters;
    form: ReactNode;
    onFilters: (filters: Filters) => void;
    onAdd: () => void;
    onEdit: (article: Article) => void;
    onDelete: (article: Article) => void;
    onLeave: (event: React.MouseEvent) => void;
}) {
    const filtered = filters.knowledgeBase !== "" || filters.text.trim() !== "";
    return (
        <main className="mx-auto flex max-w-5xl flex-col gap-4 p-4">
            <header className="flex items-center gap-2">
                <h1 className="flex-1 text-2xl font-semibold">Articles of {bot}</h1>
                <Link
                    href={`~/bots/${encodeURIComponent(bot)}`}
                    onClick={onLeave}
                    className="text-sm text-primary underline underline-offset-4"
                >
                    Bots
                </Link>
                <Button onClick={onAdd} disabled={bases.length === 0}>
                    New article
                </Button>
            </header>
            {bases.length === 0 && (
                <p className="text-muted-foreground">
                    This bot has no knowledge base yet, and an article lives in one.{" "}
                    <Link
                        href={`~/bots/${encodeURIComponent(bot)}/knowledge-bases`}
                        onClick={onLeave}
                        className="text-primary underline underline-offset-4"
                    >
                        Create the first knowledge base
                    </Link>
                    .
                </p>
            )}
            {form}
            <div className="flex gap-2">
                <select
                    aria-label="Knowledge base"
                    className={SELECT}
                    value={filters.knowledgeBase}
                    onChange={(event) => {
                        onFilters({ ...filters, knowledgeBase: event.currentTarget.value });
                    }}
                >
                    <option value="">All knowledge bases</option>
                    {bases.map((base) => (
                        <option key={base.id} value={base.id}>
                            {base.title}
                        </option>
                    ))}
                </select>
                <Input
                    aria-label="Search"
                    placeholder="Search the title, id and description"
                    value={filters.text}
                    onChange={(event) => {
                        onFilters({ ...filters, text: event.currentTarget.value });
                    }}
                />
            </div>
            {articles.length === 0 ? (
                <p className="text-muted-foreground">
                    {filtered ? "No article matches the filters." : "No articles yet. Add one: the bot answers from the articles it has."}
                </p>
            ) : (
                <ul className="flex flex-col gap-2">
                    {articles.map((article) => (
                        <li key={article.id} className="flex items-center gap-3 rounded-lg border p-3">
                            <div className="flex flex-1 flex-col gap-1">
                                <span className="font-medium">{article.title}</span>
                                <span className="text-sm text-muted-foreground">
                                    {article.id} · {article.knowledgeBase}
                                </span>
                                <span className="text-sm">{article.description}</span>
                            </div>
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                    onEdit(article);
                                }}
                            >
                                Edit
                            </Button>
                            <Button
                                variant="outline"
                                size="sm"
                                onClick={() => {
                                    onDelete(article);
                                }}
                            >
                                Delete
                            </Button>
                        </li>
                    ))}
                </ul>
            )}
        </main>
    );
}
