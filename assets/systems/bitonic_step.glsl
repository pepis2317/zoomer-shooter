#[compute]
#version 450

layout(local_size_x = 256) in;

struct SortEntry{
    uint codeLo;
    uint codeHi;
    uint index;
};

layout(set = 0, binding = 0, std430) buffer EntryBuffer{
    SortEntry entries[];
};

layout(push_constant) uniform Params {
    uint blockSize;
    uint stride;
    uint count;
    uint pad;
} params;

bool greater(SortEntry a, SortEntry b){
    if(a.codeHi != b.codeHi) return a.codeHi > b.codeHi;
    return a.codeLo > b.codeLo;
}

void main(){
    uint i = gl_GlobalInvocationID.x;
    if(i >= params.count) return;

    uint partner = i ^ params.stride;
    if(partner <= i) return;

    bool ascending = (i & params.blockSize) == 0u;

    SortEntry a = entries[i];
    SortEntry b = entries[partner];
    bool swapNeeded = ascending ? greater(a, b) : greater(b, a);
    if(swapNeeded){
        entries[i] = b;
        entries[partner] = a;
    }
}