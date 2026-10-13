using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
namespace Conquest.WorldMap {
 // Local campaign test only. Baked independently of sector rendering; does not replace engine navigation.
 public sealed class WorldMapLocalNavigation : ScriptableObject {
  public string sourceSHA256;public float cellSize=62.5f,maxSlope=25;public int columns,rows;
  public byte[] blocked;public float[] heights;
  public Vector3 Center(int i)=>new Vector3((i%columns+.5f)*cellSize,heights[i],(i/columns+.5f)*cellSize);
  public int Index(Vector3 p){if(p.x<0||p.z<0||p.x>=columns*cellSize||p.z>=rows*cellSize)return -1;return Mathf.FloorToInt(p.z/cellSize)*columns+Mathf.FloorToInt(p.x/cellSize);}
  public bool Walkable(Vector3 p){int i=Index(p);return i>=0&&blocked[i]==0;}
  public float Height(Vector3 p){float x=Mathf.Clamp(p.x/cellSize-.5f,0,columns-1),z=Mathf.Clamp(p.z/cellSize-.5f,0,rows-1);int x0=(int)x,z0=(int)z,x1=Mathf.Min(x0+1,columns-1),z1=Mathf.Min(z0+1,rows-1);return Mathf.Lerp(Mathf.Lerp(heights[z0*columns+x0],heights[z0*columns+x1],x-x0),Mathf.Lerp(heights[z1*columns+x0],heights[z1*columns+x1],x-x0),z-z0);}
  public bool SegmentClear(Vector3 a,Vector3 b){int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))/(cellSize*.2f)));int previous=-1;float previousHeight=Height(a),stepDistance=Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))/steps;for(int i=0;i<=steps;i++){var p=Vector3.Lerp(a,b,(float)i/steps);int index=Index(p);if(index<0||blocked[index]!=0)return false;float h=Height(p);if(i>0&&stepDistance>.001f&&Mathf.Abs(h-previousHeight)/stepDistance>Mathf.Tan(maxSlope*Mathf.Deg2Rad))return false;if(previous>=0&&previous%columns!=index%columns&&previous/columns!=index/columns){if(blocked[previous/columns*columns+index%columns]!=0||blocked[index/columns*columns+previous%columns]!=0)return false;}previous=index;previousHeight=h;}return true;}
  // Works without loaded Terrain colliders: the navigation height field belongs to the whole world.
  public bool Raycast(Ray ray,float maxDistance,out Vector3 point){point=default;var bounds=new Bounds(new Vector3(columns*cellSize*.5f,0,rows*cellSize*.5f),new Vector3(columns*cellSize,100000,rows*cellSize));if(!bounds.IntersectRay(ray,out float entry))return false;float step=cellSize*.5f,previous=entry;bool above=false;for(float t=entry;t<=maxDistance;t+=step){var p=ray.GetPoint(t);if(Index(p)<0){if(above)break;continue;}float difference=p.y-Height(p);if(difference>=0){above=true;previous=t;continue;}if(!above)return false;float low=previous,high=t;for(int i=0;i<14;i++){float mid=(low+high)*.5f;var q=ray.GetPoint(mid);if(q.y>Height(q))low=mid;else high=mid;}point=ray.GetPoint((low+high)*.5f);point.y=Height(point);return Index(point)>=0;}return false;}
  struct Item {public int index;public float priority;public Item(int i,float p){index=i;priority=p;}}
  sealed class Heap {readonly List<Item> items=new List<Item>();public int Count=>items.Count;
   public void Push(Item item){items.Add(item);int i=items.Count-1;while(i>0){int parent=(i-1)/2;if(items[parent].priority<=item.priority)break;items[i]=items[parent];i=parent;}items[i]=item;}
   public Item Pop(){var result=items[0];var last=items[items.Count-1];items.RemoveAt(items.Count-1);if(items.Count==0)return result;int i=0;while(i*2+1<items.Count){int child=i*2+1;if(child+1<items.Count&&items[child+1].priority<items[child].priority)child++;if(items[child].priority>=last.priority)break;items[i]=items[child];i=child;}items[i]=last;return result;}}
  public bool TryRoute(Vector3 from,Vector3 to,out Vector3[] route,out string error){Vector3[] result=null;string failure="";var search=Search(from,to,(r,e)=>{result=r;failure=e;});while(search.MoveNext()){}route=result;error=failure;return route!=null;}
  // Time slicing keeps long searches and reconstruction off a single rendering frame.
  public IEnumerator Search(Vector3 from,Vector3 to,Action<Vector3[],string> complete,int nodesPerFrame=512){int start=Index(from),end=Index(to);if(start<0||end<0||blocked[start]!=0||blocked[end]!=0){complete(null,"Destino o acceso fuera del terreno transitable.");yield break;}
   if(SegmentClear(from,to)){complete(new[]{from,to},"");yield break;}
   int count=columns*rows;var distances=new float[count];var parents=new int[count];var closed=new bool[count];for(int i=0;i<count;i++){distances[i]=float.PositiveInfinity;parents[i]=-1;}var heap=new Heap();distances[start]=0;heap.Push(new Item(start,0));bool found=false;
   if(!SegmentClear(from,Center(start))||!SegmentClear(Center(end),to)){complete(null,"El punto no tiene un acceso terrestre seguro.");yield break;}
   int expanded=0;while(heap.Count>0){int current=heap.Pop().index;if(closed[current])continue;closed[current]=true;if(current==end){found=true;break;}int x=current%columns,z=current/columns;
    for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++){if(dx==0&&dz==0)continue;int nx=x+dx,nz=z+dz;if(nx<0||nz<0||nx>=columns||nz>=rows)continue;int next=nz*columns+nx;if(blocked[next]!=0||closed[next])continue;if(dx!=0&&dz!=0&&(blocked[z*columns+nx]!=0||blocked[nz*columns+x]!=0))continue;
     float step=cellSize*(dx!=0&&dz!=0?1.41421356f:1);float slope=Mathf.Abs(heights[next]-heights[current])/step;if(slope>Mathf.Tan(maxSlope*Mathf.Deg2Rad)||!SegmentClear(Center(current),Center(next)))continue;float cost=distances[current]+step*(1+slope*2);if(cost>=distances[next])continue;distances[next]=cost;parents[next]=current;float heuristic=Vector2.Distance(new Vector2(nx,nz),new Vector2(end%columns,end/columns))*cellSize;heap.Push(new Item(next,cost+heuristic));}if(++expanded%nodesPerFrame==0)yield return null;
   }
   if(!found){complete(null,"No hay una ruta terrestre disponible.");yield break;}var points=new List<Vector3>();for(int i=end;i!=start;i=parents[i])points.Add(Center(i));points.Add(Center(start));points.Reverse();points.Insert(0,from);points.Add(to);
   var simple=new List<Vector3>{from};int anchor=0;while(anchor<points.Count-1){int next=anchor+1;for(int j=next+1;j<points.Count;j++){if(!SegmentClear(points[anchor],points[j]))break;next=j;}simple.Add(points[next]);anchor=next;yield return null;}complete(simple.ToArray(),"");
  }
 }
}
