// Where the steps are drawn. Positions are the creator's own: the server keeps them apart from the procedure.
import dagre from "@dagrejs/dagre";

import { edgesOf, stepOrder, type Draft, type Step } from "./draft";

export type Positions = Record<string, { x: number; y: number }>;

const NODE_WIDTH = 240;
const HEADER = 34;
const LINE = 20;
const RULE_ROW = 24;
const PADDING = 12;

function blockLines(step: Step): number {
    return [step.listen, step.collect, step.say, step.assign, step.start, step.inform].filter((block) => block !== undefined).length;
}

function ruleRows(step: Step): number {
    return Array.isArray(step.next) ? step.next.length : 0;
}

/** The size a step is drawn at: the node and the layout must agree, or the arranged nodes overlap. */
export function nodeSize(step: Step): { width: number; height: number } {
    return { width: NODE_WIDTH, height: HEADER + Math.max(1, blockLines(step)) * LINE + ruleRows(step) * RULE_ROW + PADDING };
}

/** Arranges the whole graph left to right, with the first step leftmost. The order of insertion is the graph's, so a draft always gives the same picture. */
export function arrange(draft: Draft): Positions {
    const graph = new dagre.graphlib.Graph();
    graph.setGraph({ rankdir: "LR", nodesep: 40, ranksep: 90, marginx: 20, marginy: 20 });
    graph.setDefaultEdgeLabel(() => ({}));
    for (const name of stepOrder(draft)) graph.setNode(name, nodeSize(draft.steps[name] ?? {}));
    for (const edge of edgesOf(draft)) graph.setEdge(edge.source, edge.target);
    dagre.layout(graph);
    return Object.fromEntries(
        Object.keys(draft.steps).map((name) => {
            const node = graph.node(name) as { x: number; y: number };
            const { width, height } = nodeSize(draft.steps[name] ?? {});
            // dagre answers with the centre of a node; the canvas wants its top left corner.
            return [name, { x: Math.round(node.x - width / 2), y: Math.round(node.y - height / 2) }];
        }),
    );
}

function collides(draft: Draft, at: { x: number; y: number }, name: string, others: Positions): boolean {
    const size = nodeSize(draft.steps[name] ?? {});
    return Object.entries(others).some(([other, position]) => {
        const against = nodeSize(draft.steps[other] ?? {});
        return !(
            at.x + size.width <= position.x ||
            position.x + against.width <= at.x ||
            at.y + size.height <= position.y ||
            position.y + against.height <= at.y
        );
    });
}

/**
 * The stored position of each step that has one, the automatic one for each that has not. An automatic position that would land on
 * a node already placed moves below the lowest node, so a step added to a graph the author arranged never hides another.
 */
export function placed(draft: Draft, stored: Positions): Positions {
    const names = stepOrder(draft);
    const result: Positions = Object.fromEntries(names.flatMap((name) => (stored[name] ? [[name, stored[name]]] : [])));
    if (Object.keys(result).length === names.length) return result;
    const automatic = arrange(draft);
    for (const name of names.filter((other) => !(other in result))) {
        const wanted = automatic[name] ?? { x: 0, y: 0 };
        const bottom = Math.max(0, ...Object.entries(result).map(([other, at]) => at.y + nodeSize(draft.steps[other] ?? {}).height));
        result[name] = collides(draft, wanted, name, result) ? { x: wanted.x, y: bottom + 40 } : wanted;
    }
    return result;
}
