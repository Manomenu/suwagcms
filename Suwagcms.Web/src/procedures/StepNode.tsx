import { Handle, Position, type Node, type NodeProps } from "@xyflow/react";

import { cn } from "@/design/classNames";

import { handlesOf, type Condition, type Step } from "./draft";
import { nodeSize } from "./layout";

interface StepData extends Record<string, unknown> {
    name: string;
    step: Step;
    first: boolean;
    flagged: boolean;
}

export type StepFlowNode = Node<StepData, "step">;

function blocksOf(step: Step): string[] {
    return [
        ...(step.listen ? ["listen"] : []),
        ...(step.collect ? [`collect: ${step.collect.join(", ") || "nothing yet"}`] : []),
        ...(step.say !== undefined ? [`say: ${step.say || "nothing yet"}`] : []),
        ...(step.assign ? [`assign: ${Object.keys(step.assign).join(", ") || "nothing yet"}`] : []),
    ];
}

function describe(fact: string, condition: Condition): string {
    if (condition.oneOf) return `${fact} in ${condition.oneOf.join("/") || "…"}`;
    if (condition.range) return `${fact} ${condition.range.min ?? "…"}–${condition.range.max ?? "…"}`;
    return `${fact} ${condition.known === false ? "unknown" : "known"}`;
}

function ruleLabel(when: Record<string, Condition> | undefined): string {
    if (when === undefined) return "otherwise";
    const parts = Object.entries(when).map(([fact, condition]) => describe(fact, condition));
    return parts.length === 0 ? "when …" : `when ${parts.join(" and ")}`;
}

/** One step: its name, its blocks, and a row with its own handle for each rule, so a drag from a row sets that rule's target. */
export function StepNode({ data, selected }: NodeProps<StepFlowNode>) {
    const { name, step, first, flagged } = data;
    const size = nodeSize(step);
    const handles = handlesOf(step);
    return (
        <div
            data-step={name}
            style={{ width: size.width, height: size.height }}
            className={cn(
                "flex flex-col rounded-lg border border-input bg-secondary text-sm text-foreground",
                flagged && "border-destructive",
                selected && "border-primary ring-1 ring-primary",
            )}
        >
            <Handle type="target" position={Position.Left} id="in" />
            <div className="flex items-center gap-1 rounded-t-lg border-b border-input bg-accent/40 px-2 py-1.5 font-medium">
                {first && (
                    <span title="The first step" aria-label="first step" className="text-primary">
                        ▶
                    </span>
                )}
                <span className="flex-1 truncate">{name}</span>
                {flagged && (
                    <span title="This step has a mistake" aria-label="has a mistake" className="text-destructive">
                        ⚠
                    </span>
                )}
            </div>
            <div className="px-2 py-1 text-xs text-muted-foreground">
                {blocksOf(step).map((line) => (
                    <div key={line} className="truncate">
                        {line}
                    </div>
                ))}
            </div>
            {Array.isArray(step.next) ? (
                <div className="mt-auto flex flex-col">
                    {step.next.map((rule, index) => (
                        <div key={handles[index]} className="relative h-6 truncate border-t border-input px-2 text-xs leading-6">
                            {ruleLabel(rule.when)} →
                            <Handle type="source" position={Position.Right} id={handles[index] ?? `rule-${index}`} />
                        </div>
                    ))}
                </div>
            ) : (
                <Handle type="source" position={Position.Right} id="next" />
            )}
        </div>
    );
}
