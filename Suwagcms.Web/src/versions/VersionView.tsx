import { historyHref, type HistoryState } from "@/api/history";
import { Alert } from "@/design/alert";
import { Button } from "@/design/button";
import { HistoryPanel } from "@/design/historyPanel";
import { Page, Paper } from "@/design/page";
import { BackToList } from "@/design/resourceEdit";
import { Select } from "@/design/select";

import { contentOf, dateOf, numberOf, type VersionComparison, type VersionDetail, type VersionSummary } from "./api";
import { ComparisonRows, comparisonLine } from "./comparison";

/** Where a comparison on the page stands: asked, answered, or refused in its place. */
export interface ComparisonState {
    with: number;
    answer: VersionComparison | null;
    refusal: string | null;
}

/** Says there is no such version, with the way back, as a resource's unknown id does. */
export function NoSuchVersion({ asked, onBack }: { asked: string; onBack: () => void }) {
    return (
        <div className="flex flex-col gap-4">
            <BackToList plural="versions" onBack={onBack} />
            <Alert variant="destructive">This bot has no version {asked}.</Alert>
        </div>
    );
}

function Compare({
    number,
    versions,
    comparison,
    onCompare,
    onClose,
}: {
    number: number;
    versions: VersionSummary[];
    comparison: ComparisonState | null;
    onCompare: (other: number) => void;
    onClose: () => void;
}) {
    const others = versions.map(numberOf).filter((other) => other !== number);
    if (others.length === 0) return null;
    return (
        <Paper className="flex flex-col gap-3">
            <div className="flex flex-wrap items-center gap-2">
                <label className="flex items-center gap-2 text-sm font-medium">
                    Compare with…
                    <Select
                        value={comparison?.with ?? ""}
                        onChange={(event) => {
                            if (event.target.value !== "") onCompare(Number(event.target.value));
                        }}
                    >
                        {comparison === null && <option value="">choose</option>}
                        {comparison !== null && !others.includes(comparison.with) && (
                            <option value={comparison.with}>v{comparison.with}</option>
                        )}
                        {others.map((other) => (
                            <option key={other} value={other}>
                                v{other}
                            </option>
                        ))}
                    </Select>
                </label>
                <span className="flex-1 text-sm text-muted-foreground">{comparison?.answer && comparisonLine(comparison.answer)}</span>
                {comparison !== null && (
                    <Button variant="outline" size="sm" onClick={onClose}>
                        Close
                    </Button>
                )}
            </div>
            {comparison?.refusal != null && <Alert variant="destructive">{comparison.refusal}</Alert>}
            {comparison?.answer != null && <ComparisonRows comparison={comparison.answer} />}
        </Paper>
    );
}

/** A version's page: what it is, what differs from any other version, and what happened between the previous version and this one. */
export function VersionView({
    bot,
    detail,
    versions,
    comparison,
    history,
    onCompare,
    onCloseComparison,
    onBack,
}: {
    bot: string;
    detail: VersionDetail;
    versions: VersionSummary[];
    comparison: ComparisonState | null;
    history: HistoryState;
    onCompare: (other: number) => void;
    onCloseComparison: () => void;
    onBack: () => void;
}) {
    const number = numberOf(detail.version);
    const day = history.since?.slice(0, 10) ?? "";
    return (
        <div className="flex flex-col gap-4">
            <BackToList plural="versions" onBack={onBack} />
            <Page title={`Version ${number}`}>
                <Paper className="flex flex-col gap-2">
                    <p className="text-sm text-muted-foreground">{dateOf(detail.version)}</p>
                    <p className="text-sm wrap-anywhere text-muted-foreground">sha256 {detail.version.sha256}</p>
                    <p className="text-sm text-muted-foreground">{contentOf(detail.version)}</p>
                    <pre className="max-h-96 overflow-auto text-xs whitespace-pre-wrap">{detail.document}</pre>
                </Paper>
                <Compare number={number} versions={versions} comparison={comparison} onCompare={onCompare} onClose={onCloseComparison} />
                <HistoryPanel
                    {...history}
                    empty="No change has been recorded for this version."
                    hrefOf={(line) => historyHref(bot, line)}
                    {...(history.incomplete
                        ? { note: `Changes made before ${day} were not recorded, so this list may be incomplete.` }
                        : {})}
                />
            </Page>
        </div>
    );
}
