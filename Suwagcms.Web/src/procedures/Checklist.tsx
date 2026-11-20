import { Legend } from "@/design/field";

export function Checklist({
    legend,
    hint,
    names,
    picked,
    labelOf,
    onChange,
}: {
    legend: string;
    hint: string;
    names: string[];
    picked: string[];
    labelOf?: (name: string) => string;
    onChange: (picked: string[]) => void;
}) {
    return (
        <fieldset className="flex flex-col gap-1">
            <Legend hint={hint}>{legend}</Legend>
            {names.map((name) => (
                <label key={name} className="flex items-center gap-2 text-sm">
                    <input
                        type="checkbox"
                        className="accent-primary"
                        checked={picked.includes(name)}
                        onChange={(event) => {
                            onChange(event.target.checked ? [...picked, name] : picked.filter((other) => other !== name));
                        }}
                    />
                    {labelOf?.(name) ?? name}
                </label>
            ))}
        </fieldset>
    );
}
