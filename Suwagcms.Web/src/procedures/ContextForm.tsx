import { Button } from "@/design/button";
import { Legend } from "@/design/field";
import { Input } from "@/design/input";

import { Checklist } from "./Checklist";
import { PROCEDURE_HINTS } from "./hints";
import { articleLabel, withStored, type Context, type LinkableArticle } from "./draft";

/** What the model should read before following an instruction: articles named by id, and queries only the bot's search can answer. */
export function ContextForm({
    context,
    articles,
    onChange,
}: {
    context: Context | undefined;
    articles: LinkableArticle[];
    onChange: (context: Context) => void;
}) {
    const queries = context?.searchQueries ?? [];
    const names = withStored(
        articles.map((article) => article.id),
        context?.articles ?? [],
    );
    return (
        <div className="flex flex-col gap-2">
            {names.length > 0 && (
                <Checklist
                    legend="Articles to read"
                    hint={PROCEDURE_HINTS.articles}
                    names={names}
                    picked={context?.articles ?? []}
                    labelOf={(id) => articleLabel(articles, id)}
                    onChange={(picked) => {
                        onChange({ ...context, articles: picked });
                    }}
                />
            )}
            <fieldset className="flex flex-col gap-1">
                <Legend hint={PROCEDURE_HINTS.searchQueries}>Search queries</Legend>
                {queries.map((query, index) => (
                    // Why: a query has no identity but its place in the list, and the list only changes through these controls.
                    <div key={index} className="flex items-center gap-1">
                        <Input
                            aria-label={`Search query ${index + 1}`}
                            value={query}
                            onChange={(event) => {
                                onChange({
                                    ...context,
                                    searchQueries: queries.map((other, at) => (at === index ? event.target.value : other)),
                                });
                            }}
                        />
                        <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            aria-label={`Remove search query ${index + 1}`}
                            onClick={() => {
                                onChange({ ...context, searchQueries: queries.filter((_, at) => at !== index) });
                            }}
                        >
                            ✕
                        </Button>
                    </div>
                ))}
                <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => {
                        onChange({ ...context, searchQueries: [...queries, ""] });
                    }}
                >
                    Add search query
                </Button>
            </fieldset>
        </div>
    );
}
