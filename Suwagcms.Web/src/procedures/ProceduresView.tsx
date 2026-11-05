import { Link } from "wouter";

import type { Mistake } from "@/api/client";

import { mistakesOf, type ProcedureList } from "./api";
import { NewProcedureForm } from "./NewProcedureForm";

function Readiness({ count }: { count: number }) {
    return count === 0 ? (
        <span className="text-sm font-medium text-green-700">ready</span>
    ) : (
        <span className="text-sm font-medium text-amber-700">
            {count} {count === 1 ? "mistake" : "mistakes"} before a version
        </span>
    );
}

/** The Procedures screen: each procedure with its description and how many mistakes still stop a version. Each opens in the creator, and a new one starts there. */
export function ProceduresView({ bot, list }: { bot: string; list: ProcedureList }) {
    const botMistakes: Mistake[] = list.botNotReady;
    return (
        <main className="mx-auto flex max-w-2xl flex-col gap-4 p-4">
            <header className="flex items-center gap-2">
                <h1 className="flex-1 text-2xl font-semibold">Procedures of {bot}</h1>
                <Link href={`~/bots/${encodeURIComponent(bot)}/facts`} className="text-sm text-primary underline underline-offset-4">
                    Facts
                </Link>
                <Link href={`~/bots/${encodeURIComponent(bot)}`} className="text-sm text-primary underline underline-offset-4">
                    Bots
                </Link>
            </header>
            {botMistakes.length > 0 && (
                <ul className="flex flex-col gap-1 rounded-lg border border-amber-300 p-3 text-sm">
                    {botMistakes.map((mistake) => (
                        <li key={mistake.code}>
                            {mistake.message} {mistake.fix}
                        </li>
                    ))}
                </ul>
            )}
            <NewProcedureForm bot={bot} />
            {list.procedures.length === 0 ? (
                <p className="text-muted-foreground">
                    No procedures yet. Create one above, or write it in a YAML file and add it with{" "}
                    <code>suwagcms procedures add --file</code>.
                </p>
            ) : (
                <ul className="flex flex-col gap-2">
                    {list.procedures.map((procedure) => (
                        <li key={procedure.id} className="flex items-center gap-3 rounded-lg border p-3">
                            <div className="flex flex-1 flex-col gap-1">
                                <Link
                                    href={`~/bots/${encodeURIComponent(bot)}/procedures/${encodeURIComponent(procedure.id)}`}
                                    className="font-medium text-primary underline underline-offset-4"
                                >
                                    {procedure.id}
                                </Link>
                                <span className="text-sm">{procedure.description}</span>
                            </div>
                            <Readiness count={mistakesOf(procedure)} />
                        </li>
                    ))}
                </ul>
            )}
        </main>
    );
}
