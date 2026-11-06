/** What a list of mistakes shows of one; `api`'s Mistake fits it, and the design system knows no API. */
export interface ShownMistake {
    code: string;
    path: string;
    message: string;
    fix: string;
}

/** The mistakes no field shows, each with its fix; the caller puts them in an alert. */
export function MistakeItems({ mistakes }: { mistakes: ShownMistake[] }) {
    return (
        <ul className="list-disc pl-4">
            {mistakes.map((mistake) => (
                <li key={`${mistake.code}:${mistake.path}`}>
                    {mistake.message} {mistake.fix}
                </li>
            ))}
        </ul>
    );
}
