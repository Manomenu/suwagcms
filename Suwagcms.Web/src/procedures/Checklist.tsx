/** A list to tick. What is ticked keeps its order, the one the author ticked it in, and anything already stored stays listed. */
export function Checklist({
    legend,
    names,
    picked,
    labelOf,
    onChange,
}: {
    legend: string;
    names: string[];
    picked: string[];
    labelOf?: (name: string) => string;
    onChange: (picked: string[]) => void;
}) {
    return (
        <fieldset className="flex flex-col gap-1">
            <legend className="text-sm font-medium">{legend}</legend>
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
