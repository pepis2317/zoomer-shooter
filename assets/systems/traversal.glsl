#[compute]
#version 450

layout(local_size_x = 256) in;

struct AABB {
    vec4 minPosition;
    vec4 maxPosition;
};

struct SortEntry {
    uint codeLo;
    uint codeHi;
    uint index;
};

struct Node {
    uint left;
    uint right;
    uint parent;
    uint pad;
};

layout(set=0, binding=0, std430) readonly buffer EntryBuffer { 
    SortEntry entries[]; 
};
layout(set=0, binding=1, std430) readonly buffer NodesBuffer { 
    Node nodes[]; 
};
layout(set=0, binding=2, std430) readonly buffer Bounds { 
    AABB bounds[]; 
};

layout(set=0, binding=3, std430) buffer PairCounter { 
    uint pairCount; 
};
layout(set=0, binding=4, std430) writeonly buffer Pairs { 
    uvec2 pairs[]; 
};

layout(push_constant) uniform Params {
    uint count;
    uint maxPairs;
    uint p1;
    uint p2;
} params;

bool overlaps(AABB a, AABB b) {
    return all(lessThanEqual(a.minPosition.xyz, b.maxPosition.xyz)) &&
           all(lessThanEqual(b.minPosition.xyz, a.maxPosition.xyz));
}

void main() {
    uint i = gl_GlobalInvocationID.x;
    uint n = params.count;
    if (i >= n) return;

    uint leafBase = n - 1u;
    uint myIndex  = entries[i].index; // original index
    AABB q = bounds[leafBase + i];

    uint stack[64];
    int sp = 0;
    stack[sp++] = 0u; // root

    while (sp > 0) {
        uint node = stack[--sp];

        if (!overlaps(q, bounds[node])) continue;

        if (node >= leafBase) {
            uint j = entries[node - leafBase].index;
            if (j > myIndex) { // skip self and duplicate (j,i) pairs
                uint slot = atomicAdd(pairCount, 1u);
                if (slot < params.maxPairs) {
                    pairs[slot] = uvec2(myIndex, j);
                }
            }
        } else {
            stack[sp++] = nodes[node].left;
            stack[sp++] = nodes[node].right;
        }
    }
}