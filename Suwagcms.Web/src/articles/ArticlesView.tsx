import { Anchor, Input, Select } from "@maniumek/suwfrontkit";
import { Link } from "wouter";
import type { ReactNode } from "react";

import { LANGUAGES } from "@/api/languages";
import { usePagedRows } from "@/api/pagination";
import { listHref } from "@/api/resourceAddress";
import { AvailabilityBadge } from "@/design/availabilityPanel";
import type { HistoryPanelProps } from "@/design/page";
import { ResourceList } from "@/design/resourceList";

import type { Article, Filters, KnowledgeBase } from "./api";

export function ArticlesView({
    bot,
    history,
    articles,
    bases,
    isOff,
    filters,
    notice,
    onFilters,
    addHref,
    hrefOf,
    onLeave,
}: {
    bot: string;
    history: HistoryPanelProps;
    articles: Article[];
    bases: KnowledgeBase[];
    isOff?: (id: string) => boolean;
    filters: Filters;
    notice?: ReactNode;
    onFilters: (filters: Filters) => void;
    addHref: string;
    hrefOf: (article: Article) => string;
    onLeave: (event: React.MouseEvent) => void;
}) {
    const paged = usePagedRows(articles);
    const filtered = filters.knowledgeBase !== "" || filters.language !== "" || filters.text.trim() !== "";
    return (
        <ResourceList
            history={history}
            title={`Articles of ${bot}`}
            addLabel="Add an article"
            addDisabled={bases.length === 0}
            addHref={addHref}
            above={
                <>
                    {bases.length === 0 && (
                        <p className="text-muted-foreground">
                            This bot has no knowledge base yet, and an article lives in one.{" "}
                            <Anchor asChild>
                                <Link href={`~${listHref(bot, "knowledge-bases")}`} onClick={onLeave}>
                                    Create the first knowledge base
                                </Link>
                            </Anchor>
                            .
                        </p>
                    )}
                    {notice}
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
                </>
            }
            rows={paged.rows}
            paging={paged.paging}
            idOf={(article) => article.id}
            hrefOf={hrefOf}
            columns={[
                { header: "Title", className: "font-semibold", cell: (article) => article.title },
                { header: "Id", className: "text-muted-foreground", cell: (article) => article.id },
                { header: "Knowledge base", cell: (article) => article.knowledgeBase },
                { header: "Language", cell: (article) => article.language },
                { header: "Description", className: "text-muted-foreground", cell: (article) => article.description },
                { header: "Availability", cell: (article) => <AvailabilityBadge off={isOff?.(article.id) ?? false} /> },
            ]}
            empty={
                <p className="text-muted-foreground">
                    {filtered ? "No article matches the filters." : "No articles yet. Add one: the bot answers from the articles it has."}
                </p>
            }
        />
    );
}
