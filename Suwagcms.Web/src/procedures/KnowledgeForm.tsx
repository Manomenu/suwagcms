import { Button, Input, Legend, Select } from "@maniumek/suwfrontkit";
import type { ReactNode } from "react";

import { Checklist } from "./Checklist";
import { articlesOf, basesOffered, withBase, withStored, type KnowledgeEntry, type LinkableArticle, type LinkableBase } from "./draft";
import { PROCEDURE_HINTS } from "./hints";

export type Beside = (place: string) => ReactNode;

function ArticleChecklist({
    entry,
    articles,
    onChange,
}: {
    entry: KnowledgeEntry;
    articles: LinkableArticle[];
    onChange: (picked: string[]) => void;
}) {
    const own = articlesOf(articles, entry.knowledgeBase);
    const names = withStored(
        own.map((article) => article.id),
        entry.articles ?? [],
    );
    if (names.length === 0) return <p className="text-sm text-muted-foreground">This knowledge base has no articles yet.</p>;
    return (
        <Checklist
            legend="Articles to read"
            hint={PROCEDURE_HINTS.articles}
            names={names}
            picked={entry.articles ?? []}
            labelOf={(id) => {
                const title = articles.find((article) => article.id === id)?.title;
                return title === undefined ? id : `${title} (${id})`;
            }}
            onChange={onChange}
        />
    );
}

function Queries({ queries, onChange }: { queries: string[]; onChange: (queries: string[]) => void }) {
    return (
        <fieldset className="flex flex-col gap-1">
            <Legend hint={PROCEDURE_HINTS.searchQueries}>Search queries</Legend>
            {queries.map((query, index) => (
                <div key={index} className="flex items-center gap-1">
                    <Input
                        aria-label={`Search query ${index + 1}`}
                        value={query}
                        onChange={(event) => {
                            onChange(queries.map((other, at) => (at === index ? event.target.value : other)));
                        }}
                    />
                    <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        aria-label={`Remove search query ${index + 1}`}
                        onClick={() => {
                            onChange(queries.filter((_, at) => at !== index));
                        }}
                    >
                        ✕
                    </Button>
                </div>
            ))}
            <div>
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => {
                        onChange([...queries, ""]);
                    }}
                >
                    + Add query
                </Button>
            </div>
        </fieldset>
    );
}

export function KnowledgeForm({
    owner,
    show,
    knowledge,
    bases,
    articles,
    beside,
    onChange,
}: {
    owner: string;
    show: boolean;
    knowledge: KnowledgeEntry[];
    bases: LinkableBase[];
    articles: LinkableArticle[];
    beside: Beside;
    onChange: (knowledge: KnowledgeEntry[]) => void;
}) {
    if (!show && knowledge.length === 0) return null;
    const free = basesOffered(bases, knowledge);
    const change = (index: number, entry: KnowledgeEntry) => {
        onChange(knowledge.map((other, at) => (at === index ? entry : other)));
    };
    return (
        <fieldset className="flex flex-col gap-2 rounded-md border border-border p-3" aria-label="Knowledge">
            <Legend hint={PROCEDURE_HINTS.knowledge}>Knowledge</Legend>
            {knowledge.map((entry, index) => {
                const options = basesOffered(bases, knowledge, entry.knowledgeBase);
                const listed = options.some((one) => one.id === entry.knowledgeBase);
                return (
                    <div key={index} className="flex flex-col gap-2" data-place={`knowledge:${owner}:${index}`}>
                        <div className="flex items-center gap-2 text-sm">
                            <Select
                                aria-label={`Knowledge base of entry ${index + 1}`}
                                value={entry.knowledgeBase}
                                onChange={(event) => {
                                    change(index, withBase(entry, event.target.value));
                                }}
                            >
                                {!listed && <option value={entry.knowledgeBase}>{entry.knowledgeBase || "(choose)"}</option>}
                                {options.map((one) => (
                                    <option key={one.id} value={one.id}>
                                        {one.title || one.id}
                                    </option>
                                ))}
                            </Select>
                            <Button
                                type="button"
                                variant="ghost"
                                size="sm"
                                onClick={() => {
                                    onChange(knowledge.filter((_, at) => at !== index));
                                }}
                            >
                                Remove
                            </Button>
                        </div>
                        <ArticleChecklist
                            entry={entry}
                            articles={articles}
                            onChange={(picked) => {
                                change(index, { ...entry, articles: picked });
                            }}
                        />
                        <Queries
                            queries={entry.searchQueries ?? []}
                            onChange={(searchQueries) => {
                                change(index, { ...entry, searchQueries });
                            }}
                        />
                        {beside(`knowledge:${owner}:${index}`)}
                    </div>
                );
            })}
            {bases.length === 0 && <p className="text-sm text-muted-foreground">This bot has no knowledge bases yet.</p>}
            {free[0] !== undefined && (
                <Select
                    aria-label="Add knowledge"
                    value=""
                    onChange={(event) => {
                        if (event.target.value !== "") onChange([...knowledge, { knowledgeBase: event.target.value }]);
                    }}
                >
                    <option value="">+ Add knowledge…</option>
                    {free.map((one) => (
                        <option key={one.id} value={one.id}>
                            {one.title || one.id}
                        </option>
                    ))}
                </Select>
            )}
        </fieldset>
    );
}
