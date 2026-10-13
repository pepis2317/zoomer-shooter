#[compute]
#version 450

layout(local_size_x = 256) in;

struct SortEntry{
    uint codeLo;
    uint codeHi;
    uint index;
};

struct Node{
    uint left;
    uint right;
    uint parent;
    uint pad;
};

layout(set=0, binding=0, std430) readonly buffer EntryBuffer{
    SortEntry entries[];
};

layout(set=0, binding=1, std430) buffer NodesBuffer{
    Node nodes[];
};

layout(push_constant) uniform Params{
    uint count;
    uint p0;
    uint p1;
    uint p2;
} params;

int n;

int delta(int i, int j){
    if( j < 0 || j >= n ) return -1;
    SortEntry a = entries[i];
    SortEntry b = entries[j];
    if(a.codeHi != b.codeHi){
        return 31 - findMSB(a.codeHi ^ b.codeHi);
    }
    if(a.codeLo != b.codeLo){
        return 32 + 31 - findMSB(a.codeLo ^ b.codeLo);
    }
    return 64 + 31 - findMSB(uint(i) ^ uint(j));
}

void main(){
    int i = int(gl_GlobalInvocationID.x);
    n = int(params.count);
    if(i >= n-1) return;

    // direction of the range
    int d = delta(i, i + 1) - delta(i, i - 1) > 0 ? 1 : -1;
    int dmin = delta(i, i - d);

    // upper bound on range length
    int lmax = 2;
    while(delta(i, i+ lmax * d) > dmin){
        lmax *= 2;
    }

    // binary search for the other end
    int l = 0;
    for(int t = lmax/2; t >= 1; t /= 2){
        if(delta(i, i + (l + t) * d) > dmin){
            l += t;
        }
    }
    int j = i + l * d;

    //binary search for the split position
    int dnode = delta(i, j);
    int s = 0;
    int t = l;
    do{
        t = (t + 1) >> 1;
        if(delta(i, i + (s + t) * d) > dnode){
            s += t;
        }
    }while(t > 1);
    int gamma = i + s * d + min(d, 0);

    int leafBase = n - 1;
    uint left  = (min(i, j) == gamma)     ? uint(leafBase + gamma)     : uint(gamma);
    uint right = (max(i, j) == gamma + 1) ? uint(leafBase + gamma + 1) : uint(gamma + 1);

    nodes[i].left = left;
    nodes[i].right = right;
    nodes[left].parent = uint(i);
    nodes[right].parent = uint(i);
    if (i == 0) nodes[0].parent = 0xFFFFFFFFu; // root


}