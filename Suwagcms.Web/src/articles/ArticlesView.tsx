import type { ReactNode } from "react";

import { LANGUAGES } from "@/api/languages";
import { Anchor } from "@/design/anchor";
import { Button } from "@/design/button";
import { Input } from "@/design/input";
import { Page, Paper } from "@/design/page";
import { Select } from "@/design/select";
import { Table } from "@/design/table";

import type { Article, Filters, KnowledgeBase } from "./api";

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
    onLeave: (event: React.MouseEvent) => void;
}) {
    const filtered = filters.knowledgeBase !== "" || filters.language !== "" || filters.text.trim() !== "";
    return (
        <Page
            title={`Articles of ${bot}`}
            actions={
                <Button onClick={onAdd} disabled={bases.length === 0}>
                    New article
                </Button>
            }
        >
            {bases.length === 0 && (
                <p className="text-muted-foreground">
                    This bot has no knowledge base yet, and an article lives in one.{" "}
                    <Anchor href={`~/bots/${encodeURIComponent(bot)}/knowledge-bases`} onClick={onLeave}>
                        Create the first knowledge base
                    </Anchor>
                    .
                </p>
            )}
            {form}
            <div className="flex gap-2">
                <Select
                    aria-label="Knowledge base"
                    className="shrink-0"
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
                </Select>
                <Select
                    aria-label="Language"
                    className="shrink-0"
                    value={filters.language}
                    onChange={(event) => {
                        onFilters({ ...filters, language: event.currentTarget.value });
                    }}
                >
                    <option value="">All languages</option>
                    {LANGUAGES.map((language) => (
                        <option key={language} value={language}>
                            {language}
                        </option>
                    ))}
                </Select>
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
                <Paper>
                    <Table>
                        <thead>
                            <tr>
                                <th>Title</th>
                                <th>Id</th>
                                <th>Knowledge base</th>
                                <th>Language</th>
                                <th>Description</th>
                                <th />
                            </tr>
                        </thead>
                        <tbody>
                            {articles.map((article) => (
                                <tr key={article.id}>
                                    <td className="font-semibold">{article.title}</td>
                                    <td className="text-muted-foreground">{article.id}</td>
                                    <td>{article.knowledgeBase}</td>
                                    <td>{article.language}</td>
                                    <td className="text-muted-foreground">{article.description}</td>
                                    <td className="text-right">
                                        <Button
                                            variant="outline"
                                            size="sm"
                                            onClick={() => {
                                                onEdit(article);
                                            }}
                                        >
                                            Edit
                                        </Button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </Table>
                </Paper>
            )}
        </Page>
    );
}
