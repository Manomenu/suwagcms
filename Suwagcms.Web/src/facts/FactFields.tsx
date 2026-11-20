import { Anchor, Field as DesignField, Input, Select } from "@maniumek/suwfrontkit";
import { Link } from "wouter";
import type { ReactNode } from "react";

import type { Mistake } from "@/api/client";
import { usageHref } from "@/api/usage";
import { ConstraintFields, PatternFields } from "@/design/constraintFields";
import { FACT_HINTS } from "@/design/factHints";

import { fieldOf, referrerPlace, type FactForm, type FieldName } from "./factForm";

const NAME_HINT =
    "The fact's id: how procedures refer to it. It may have dots, like customer.city. A global fact lives for the whole conversation. To change it later, use Change name.";

function Referrers({ bot, mistakes }: { bot: string; mistakes: Mistake[] }) {
    const places = mistakes.flatMap((mistake) => mistake.related).filter((related) => related.resource.kind === "Procedure");
    if (places.length === 0) return null;
    return (
        <ul aria-label="Used by" className="flex flex-col gap-1 pl-4 text-sm">
            {places.map((place) => (
                <li key={`${place.resource.id}:${place.location}`}>
                    <Anchor asChild>
                        <Link href={usageHref(bot, { kind: "procedure", id: place.resource.id, field: place.location })}>
                            {place.resource.id}
                        </Link>
                    </Anchor>{" "}
                    <span className="text-muted-foreground">{referrerPlace(place.location)}</span>
                </li>
            ))}
        </ul>
    );
}

function Field({
    bot,
    field,
    label,
    hint,
    mistakes,
    children,
}: {
    bot: string;
    field: FieldName;
    label: string;
    hint: string;
    mistakes: Mistake[];
    children: (props: object) => ReactNode;
}) {
    const own = mistakes.filter((mistake) => fieldOf(mistake) === field);
    return (
        <div className="flex flex-col gap-2">
            <DesignField
                prefix="fact"
                name={field}
                label={label}
                hint={hint}
                errors={own.map((mistake) => `${mistake.message} ${mistake.fix}`)}
            >
                {children}
            </DesignField>
            <Referrers bot={bot} mistakes={own} />
        </div>
    );
}

export function FactFields({
    bot,
    form,
    mistakes,
    nameLocked,
    onChange,
}: {
    bot: string;
    form: FactForm;
    mistakes: Mistake[];
    nameLocked: boolean;
    onChange: (change: Partial<FactForm>) => void;
}) {
    const field = (name: FieldName, label: string, hint: string, control: (props: object) => ReactNode) => (
        <Field bot={bot} field={name} label={label} hint={hint} mistakes={mistakes}>
            {control}
        </Field>
    );
    return (
        <>
            <Field bot={bot} field="name" label="Name" hint={NAME_HINT} mistakes={mistakes}>
                {(props) => (
                    <Input
                        {...props}
                        value={form.name}
                        disabled={nameLocked}
                        placeholder="customer.city"
                        onChange={(event) => {
                            onChange({ name: event.currentTarget.value });
                        }}
                    />
                )}
            </Field>
            <Field bot={bot} field="type" label="Type" hint={FACT_HINTS.type} mistakes={mistakes}>
                {(props) => (
                    <Select
                        {...props}
                        className="w-full"
                        value={form.type}
                        onChange={(event) => {
                            onChange({ type: event.currentTarget.value });
                        }}
                    >
                        <option value="text">text</option>
                        <option value="enum">enum</option>
                        <option value="int">int</option>
                    </Select>
                )}
            </Field>
            <ConstraintFields form={form} field={field} onChange={onChange} />
            {form.type === "text" && <PatternFields form={form} field={field} onChange={onChange} />}
            <Field bot={bot} field="description" label="Description" hint={FACT_HINTS.description} mistakes={mistakes}>
                {(props) => (
                    <Input
                        {...props}
                        value={form.description}
                        placeholder="The city the customer lives in"
                        onChange={(event) => {
                            onChange({ description: event.currentTarget.value });
                        }}
                    />
                )}
            </Field>
            <Field bot={bot} field="writableBy" label="Writable by" hint={FACT_HINTS.writableBy} mistakes={mistakes}>
                {(props) => (
                    <Select
                        {...props}
                        className="w-full"
                        value={form.writableBy}
                        onChange={(event) => {
                            onChange({ writableBy: event.currentTarget.value });
                        }}
                    >
                        <option value="assign">assign</option>
                        <option value="set">set</option>
                        <option value="correct">correct</option>
                    </Select>
                )}
            </Field>
        </>
    );
}
