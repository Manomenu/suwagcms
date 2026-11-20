import { Alert, Badge, Button, Paper } from "@maniumek/suwfrontkit";
import type { DeclarationStatus } from "@/api/declaration";

function whenOf(at: string): string {
    return `${at.slice(0, 16).replace("T", " ")} UTC`;
}

function askedLine(status: DeclarationStatus): string {
    if (status.ask === "Answered") return "Asked just now";
    return status.answeredAt === null ? "Not asked yet" : `Last answer, from ${whenOf(status.answeredAt)}`;
}

function Problem({ status }: { status: DeclarationStatus }) {
    if (status.ask === "Unreachable") {
        return (
            <Alert variant="destructive">
                {status.reason}
                {status.stored === "NeverAnswered"
                    ? ", and it has never answered. Nothing is checked against a declaration until it does."
                    : ". The last answer stays. Check that the bot is running and that the CMS can reach it, then ask again."}
            </Alert>
        );
    }
    if (status.ask === "Invalid") {
        return (
            <Alert variant="destructive">
                <div className="flex flex-col gap-1">
                    <p className="font-medium">The bot's answer was refused; what the CMS holds stays as it was.</p>
                    <ul className="flex flex-col gap-1">
                        {status.mistakes.map((mistake, index) => (
                            <li key={`${mistake.code}-${index}`}>
                                {mistake.message} {mistake.fix}
                            </li>
                        ))}
                    </ul>
                </div>
            </Alert>
        );
    }
    return null;
}

function Declared({ status }: { status: DeclarationStatus }) {
    const declaration = status.declaration;
    if (declaration === null) return null;
    return (
        <dl className="grid grid-cols-[max-content_1fr] items-center gap-x-4 gap-y-2 text-sm">
            <dt className="text-muted-foreground">Format version</dt>
            <dd>{declaration.formatVersion === null ? "" : Number(declaration.formatVersion)}</dd>
            <dt className="text-muted-foreground">Blocks</dt>
            <dd className="flex flex-wrap gap-1">
                {(declaration.blocks ?? []).map((block) => (
                    <Badge key={block} variant="primary">
                        {block}
                    </Badge>
                ))}
            </dd>
            <dt className="text-muted-foreground">Actions in its code</dt>
            <dd className="flex flex-col gap-1">
                {(declaration.codeActions ?? []).length === 0 && <span className="text-muted-foreground">None</span>}
                {(declaration.codeActions ?? []).map((action) => (
                    <span key={action.id}>
                        <code>{action.id}</code> <span className="text-muted-foreground">{action.description}</span>
                    </span>
                ))}
            </dd>
            {declaration.knowledge === true && (
                <>
                    <dt className="text-muted-foreground">Knowledge</dt>
                    <dd>Reads an instruction's knowledge</dd>
                </>
            )}
            {declaration.persona === "required" && (
                <>
                    <dt className="text-muted-foreground">Persona</dt>
                    <dd>Required: a version without one is refused</dd>
                </>
            )}
            {declaration.persona === "complete" && (
                <>
                    <dt className="text-muted-foreground">Persona</dt>
                    <dd>Complete: a version with any part left empty is refused</dd>
                </>
            )}
        </dl>
    );
}

export function DeclarationView({
    status,
    asking,
    failure,
    onAsk,
}: {
    status: DeclarationStatus | null;
    asking: boolean;
    failure: string | null;
    onAsk: () => void;
}) {
    return (
        <Paper className="flex flex-col gap-3">
            <div className="flex items-center gap-3">
                <h2 className="flex-1 text-xl font-bold">What it supports</h2>
                {status !== null && <span className="text-sm text-muted-foreground">{askedLine(status)}</span>}
                <Button variant="outline" size="sm" disabled={asking} onClick={onAsk}>
                    {asking ? "Asking…" : "Ask again"}
                </Button>
            </div>
            {failure !== null && <Alert variant="destructive">{failure}</Alert>}
            {status === null && failure === null && <p className="text-sm text-muted-foreground">Asking the bot…</p>}
            {status !== null && (
                <>
                    <Problem status={status} />
                    {status.stored === "DeclaresNothing" && (
                        <p className="text-sm text-muted-foreground">
                            This bot declares nothing, so nothing is checked against a declaration and the editor offers every block.
                        </p>
                    )}
                    <Declared status={status} />
                    {status.formatFix !== null && <Alert variant="warning">{status.formatFix}</Alert>}
                    {status.cannotRun.length > 0 && (
                        <Alert variant="warning">
                            <div className="flex flex-col gap-1">
                                <p className="font-medium">Procedures using a block the bot does not declare</p>
                                <ul className="flex flex-col gap-1">
                                    {status.cannotRun.map((mistake) => (
                                        <li key={`${mistake.resource.id}-${mistake.path}`}>{mistake.message}</li>
                                    ))}
                                </ul>
                            </div>
                        </Alert>
                    )}
                </>
            )}
        </Paper>
    );
}
