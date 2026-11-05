import type { ReactNode } from "react";

import type { Mistake } from "@/api/client";
import { usageHref } from "@/api/usage";
import { Anchor } from "@/design/anchor";
import { Field as DesignField } from "@/design/field";
import { Input } from "@/design/input";
import { Select } from "@/design/select";

import { fieldOf, referrerPlace, type FactForm, type FieldName } from "./factForm";

/** The steps a refusal names, each a link to its procedure, written under the mistakes of the field. */
function Referrers({ bot, mistakes }: { bot: string; mistakes: Mistake[] }) {
    const places = mistakes.flatMap((mistake) => mistake.related).filter((related) => related.resource.kind === "Procedure");
    if (places.length === 0) return null;
    return (
        <ul aria-label="Used by" className="flex flex-col gap-1 pl-4 text-sm">
            {places.map((place) => (
                <li key={`${place.resource.id}:${place.location}`}>
                    <Anchor href={usageHref(bot, { kind: "procedure", id: place.resource.id, field: place.location })}>
                        {place.resource.id}
                    </Anchor>{" "}
                    <span className="text-muted-foreground">{referrerPlace(place.location)}</span>
                </li>
            ))}
        </ul>
    );
}

/** One field of the fact form with the mistakes its path names written beneath it, and the steps they name. */
function Field({
    bot,
    field,
    label,
    mistakes,
    children,
}: {
    bot: string;
    field: FieldName;
    label: string;
    mistakes: Mistake[];
    children: (props: object) => ReactNode;
}) {
    const own = mistakes.filter((mistake) => fieldOf(mistake) === field);
    return (
        <div className="flex flex-col gap-2">
            <DesignField prefix="fact" name={field} label={label} errors={own.map((mistake) => `${mistake.message} ${mistake.fix}`)}>
                {children}
            </DesignField>
            <Referrers bot={bot} mistakes={own} />
        </div>
    );
}

/** The fields of the fact form; a mistake shows beside the field its path names. */
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
    return (
        <>
            <Field bot={bot} field="name" label="Name" mistakes={mistakes}>
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
            <Field bot={bot} field="type" label="Type" mistakes={mistakes}>
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
            {form.type === "enum" && (
                <Field bot={bot} field="values" label="Values (separated by commas)" mistakes={mistakes}>
                    {(props) => (
                        <Input
                            {...props}
                            value={form.values}
                            placeholder="house, flat"
                            onChange={(event) => {
                                onChange({ values: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
            )}
            {form.type === "int" && (
                <>
                    <Field bot={bot} field="min" label="Minimum" mistakes={mistakes}>
                        {(props) => (
                            <Input
                                {...props}
                                inputMode="numeric"
                                value={form.min}
                                onChange={(event) => {
                                    onChange({ min: event.currentTarget.value });
                                }}
                            />
                        )}
                    </Field>
                    <Field bot={bot} field="max" label="Maximum" mistakes={mistakes}>
                        {(props) => (
                            <Input
                                {...props}
                                inputMode="numeric"
                                value={form.max}
                                onChange={(event) => {
                                    onChange({ max: event.currentTarget.value });
                                }}
                            />
                        )}
                    </Field>
                </>
            )}
            {form.type === "text" && (
                <Field bot={bot} field="maxLength" label="Maximum length" mistakes={mistakes}>
                    {(props) => (
                        <Input
                            {...props}
                            inputMode="numeric"
                            value={form.maxLength}
                            onChange={(event) => {
                                onChange({ maxLength: event.currentTarget.value });
                            }}
                        />
                    )}
                </Field>
            )}
            <Field bot={bot} field="description" label="Description" mistakes={mistakes}>
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
            <Field bot={bot} field="writableBy" label="Writable by" mistakes={mistakes}>
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
