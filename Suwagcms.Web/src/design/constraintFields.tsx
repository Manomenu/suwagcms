import type { ReactNode } from "react";

import { CONSTRAINT_HINTS, PATTERN_HINTS } from "@/design/factHints";
import { Input } from "@/design/input";
import { Select } from "@/design/select";

/** Puts one input in its field. Each form has its own, since where a mistake shows differs on purpose: beside the field, or in the procedure's live list. */
export type FieldWrapper<Name extends string> = (
    name: Name,
    label: string,
    hint: string,
    control: (props: object) => ReactNode,
) => ReactNode;

export interface ConstraintValues {
    type: string;
    values: string;
    min: string;
    max: string;
    maxLength: string;
}

/** The inputs of what a type constrains: an enum's values, an int's bounds, a text's length. Bounds are typed on a number pad. */
export function ConstraintFields({
    form,
    field,
    onChange,
}: {
    form: ConstraintValues;
    field: FieldWrapper<"values" | "min" | "max" | "maxLength">;
    onChange: (change: Partial<ConstraintValues>) => void;
}) {
    const whole = (name: "min" | "max" | "maxLength", label: string) =>
        field(name, label, CONSTRAINT_HINTS[name], (props) => (
            <Input
                {...props}
                inputMode="numeric"
                value={form[name]}
                onChange={(event) => {
                    onChange({ [name]: event.currentTarget.value });
                }}
            />
        ));
    if (form.type === "enum")
        return field("values", "Values (separated by commas)", CONSTRAINT_HINTS.values, (props) => (
            <Input
                {...props}
                value={form.values}
                placeholder="house, flat"
                onChange={(event) => {
                    onChange({ values: event.currentTarget.value });
                }}
            />
        ));
    if (form.type === "int")
        return (
            <>
                {whole("min", "Minimum")}
                {whole("max", "Maximum")}
            </>
        );
    return form.type === "text" ? whole("maxLength", "Maximum length") : null;
}

export interface PatternValues {
    patternRegex: string;
    patternMeans: string;
    patternExample: string;
    patternTellCustomer: string;
}

/** The inputs of a text fact's pattern, the same on a global fact and on a local one. */
export function PatternFields({
    form,
    field,
    onChange,
}: {
    form: PatternValues;
    field: FieldWrapper<"pattern.regex" | "pattern.means" | "pattern.example" | "pattern.tellCustomer">;
    onChange: (change: Partial<PatternValues>) => void;
}) {
    return (
        <>
            {field("pattern.regex", "Pattern: regular expression", PATTERN_HINTS.regex, (props) => (
                <Input
                    {...props}
                    value={form.patternRegex}
                    placeholder="Left empty: any text"
                    onChange={(event) => {
                        onChange({ patternRegex: event.currentTarget.value });
                    }}
                />
            ))}
            {field("pattern.means", "Pattern: what it means", PATTERN_HINTS.means, (props) => (
                <Input
                    {...props}
                    value={form.patternMeans}
                    onChange={(event) => {
                        onChange({ patternMeans: event.currentTarget.value });
                    }}
                />
            ))}
            {field("pattern.example", "Pattern: example", PATTERN_HINTS.example, (props) => (
                <Input
                    {...props}
                    value={form.patternExample}
                    onChange={(event) => {
                        onChange({ patternExample: event.currentTarget.value });
                    }}
                />
            ))}
            {field("pattern.tellCustomer", "Pattern: tell the customer", PATTERN_HINTS.tellCustomer, (props) => (
                <Select
                    {...props}
                    className="w-full"
                    value={form.patternTellCustomer}
                    onChange={(event) => {
                        onChange({ patternTellCustomer: event.currentTarget.value });
                    }}
                >
                    <option value="upfront">upfront</option>
                    <option value="afterRefusal">afterRefusal</option>
                </Select>
            ))}
        </>
    );
}
