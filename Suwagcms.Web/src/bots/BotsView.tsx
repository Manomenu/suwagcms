import type { ReactNode } from "react";

import type { Bot } from "@/api/bots";
import { Anchor } from "@/design/anchor";
import { Button } from "@/design/button";
import { Page, Paper } from "@/design/page";
import { Table } from "@/design/table";

export function profilePath(id: string): string {
    return `/bots/${encodeURIComponent(id)}`;
}

function EmptyState({ onRegister }: { onRegister: () => void }) {
    return (
        <div className="flex flex-col items-start gap-3">
            <p className="text-muted-foreground">No bots yet. Register one to manage its knowledge, procedures and facts.</p>
            <Button onClick={onRegister}>Register a bot</Button>
        </div>
    );
}

function MissingBot({ id }: { id: string }) {
    return (
        <div className="flex flex-col items-start gap-3">
            <h2 className="text-xl font-bold">No bot '{id}'</h2>
            <p className="text-muted-foreground">There is no bot with this id. It may be mistyped, or not registered yet.</p>
            <Anchor href="~/bots">Bots</Anchor>
        </div>
    );
}

export function BotsView({
    bots,
    openId,
    onRegister,
    registerForm,
    declarationPanel,
}: {
    bots: Bot[];
    openId: string | null;
    onRegister: () => void;
    registerForm: ReactNode;
    declarationPanel?: ReactNode;
}) {
    const open = bots.find((bot) => bot.id === openId);

    if (openId !== null && open === undefined) return <MissingBot id={openId} />;

    if (open !== undefined) {
        return (
            <Page title={open.name}>
                <Paper>
                    <p className="text-sm text-muted-foreground">{open.address}</p>
                </Paper>
                {declarationPanel}
                <p className="text-muted-foreground">Choose Facts, Knowledge bases, Articles, Procedures or Versions in the panel.</p>
            </Page>
        );
    }

    return (
        <Page
            title="Bots"
            actions={bots.length > 0 && registerForm === null ? <Button onClick={onRegister}>Register a bot</Button> : undefined}
        >
            {registerForm !== null && <Paper>{registerForm}</Paper>}
            {bots.length === 0 && registerForm === null && <EmptyState onRegister={onRegister} />}
            {bots.length > 0 && (
                <>
                    <p className="text-muted-foreground">Choose a bot from the panel to work on it, or register a new one.</p>
                    <Paper>
                        <Table>
                            <thead>
                                <tr>
                                    <th>Name</th>
                                    <th>Address</th>
                                </tr>
                            </thead>
                            <tbody>
                                {bots.map((bot) => (
                                    <tr key={bot.id}>
                                        <td>
                                            <Anchor href={`~${profilePath(bot.id)}`}>{bot.name}</Anchor>
                                        </td>
                                        <td className="text-muted-foreground">{bot.address}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </Table>
                    </Paper>
                </>
            )}
        </Page>
    );
}
