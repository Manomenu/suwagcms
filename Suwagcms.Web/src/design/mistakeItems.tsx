export interface ShownMistake {
    code: string;
    path: string;
    message: string;
    fix: string;
}

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
