using System;
using System.Collections.Generic;
using UnityEngine;
namespace Conquest.WorldMap {
 // Local campaign test only. Baked independently of sector rendering; does not replace engine navigation.
 public sealed class WorldMapLocalNavigation : ScriptableObject {
  public string sourceSHA256;public float cellSize=62.5f,maxSlope=25;public int columns,rows;
  public byte[] blocked;public float[] heights;
  public Vector3 Center(int i)=>new Vector3((i%columns+.5f)*cellSize,heights[i],(i/columns+.5f)*cellSize);
  public int Index(Vector3 p){if(p.x<0||p.z<0||p.x>=columns*cellSize||p.z>=rows*cellSize)return -1;return Mathf.FloorToInt(p.z/cellSize)*columns+Mathf.FloorToInt(p.x/cellSize);}
  public bool Walkable(Vector3 p){int i=Index(p);return i>=0&&blocked[i]==0;}
  public bool SegmentClear(Vector3 a,Vector3 b){int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))/(cellSize*.2f)));int previous=-1;for(int i=0;i<=steps;i++){int index=Index(Vector3.Lerp(a,b,(float)i/steps));if(index<0||blocked[index]!=0)return false;if(previous>=0&&previous%columns!=index%columns&&previous/columns!=index/columns){if(blocked[previous/columns*columns+index%columns]!=0||blocked[index/columns*columns+previous%columns]!=0)return false;}previous=index;}return true;}
  struct Item {public int index;public float priority;public Item(int i,float p){index=i;priority=p;}}
  sealed class Heap {readonly List<Item> items=new List<Item>();public int Count=>items.Count;
   public void Push(Item item){items.Add(item);int i=items.Count-1;while(i>0){int parent=(i-1)/2;if(items[parent].priority<=item.priority)break;items[i]=items[parent];i=parent;}items[i]=item;}
   public Item Pop(){var result=items[0];var last=items[items.Count-1];items.RemoveAt(items.Count-1);if(items.Count==0)return result;int i=0;while(i*2+1<items.Count){int child=i*2+1;if(child+1<items.Count&&items[child+1].priority<items[child].priority)child++;if(items[child].priority>=last.priority)break;items[i]=items[child];i=child;}items[i]=last;return result;}}
  public bool TryRoute(Vector3 from,Vector3 to,out Vector3[] route,out string error){route=null;error="";int start=Index(from),end=Index(to);if(start<0||end<0||blocked[start]!=0||blocked[end]!=0){error="Destino o acceso fuera del terreno transitable.";return false;}
   if(SegmentClear(from,to)){route=new[]{from,to};return true;}
   int count=columns*rows;var distances=new float[count];var parents=new int[count];var closed=new bool[count];for(int i=0;i<count;i++){distances[i]=float.PositiveInfinity;parents[i]=-1;}var heap=new Heap();distances[start]=0;heap.Push(new Item(start,0));bool found=false;
   while(heap.Count>0){int current=heap.Pop().index;if(closed[current])continue;closed[current]=true;if(current==end){found=true;break;}int x=current%columns,z=current/columns;
    for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++){if(dx==0&&dz==0)continue;int nx=x+dx,nz=z+dz;if(nx<0||nz<0||nx>=columns||nz>=rows)continue;int next=nz*columns+nx;if(blocked[next]!=0||closed[next])continue;if(dx!=0&&dz!=0&&(blocked[z*columns+nx]!=0||blocked[nz*columns+x]!=0))continue;
     float step=cellSize*(dx!=0&&dz!=0?1.41421356f:1);float slope=Mathf.Abs(heights[next]-heights[current])/step;if(slope>Mathf.Tan(maxSlope*Mathf.Deg2Rad))continue;float cost=distances[current]+step*(1+slope*2);if(cost>=distances[next])continue;distances[next]=cost;parents[next]=current;float heuristic=Vector2.Distance(new Vector2(nx,nz),new Vector2(end%columns,end/columns))*cellSize;heap.Push(new Item(next,cost+heuristic));}
   }
   if(!found){error="No hay una ruta terrestre disponible.";return false;}var points=new List<Vector3>();for(int i=end;i!=start;i=parents[i])points.Add(Center(i));points.Add(Center(start));points.Reverse();points.Insert(0,from);points.Add(to);
   var simple=new List<Vector3>{from};int anchor=0;while(anchor<points.Count-1){int next=anchor+1;for(int j=next+1;j<points.Count;j++){if(!SegmentClear(points[anchor],points[j]))break;next=j;}simple.Add(points[next]);anchor=next;}route=simple.ToArray();return true;
  }
 }
}
