#include "breez_ui.h"
#include <cstdio>
#include <chrono>
static uint16_t FB[240*240], BAND[240*40];
template<class F> void frame(const char*n, F f){
  for(int y=0;y<240;y+=40){ ui::setBand(BAND,y,40); f(); memcpy(FB+y*240,BAND,240*40*2);} 
  FILE*o=fopen(n,"wb"); fwrite(FB,2,240*240,o); fclose(o);
}
int main(){
  ui::begin(BAND,40);
  frame("idle.raw",[]{ui::drawIdle(1500,0,12);});
  frame("idle_blow.raw",[]{ui::drawIdle(3000,55,12);});
  frame("cal.raw",[]{ui::drawCalibrating(300);});
  frame("ready.raw",[]{ui::drawReady(500,3,0.1f);});
  frame("inhale.raw",[]{ui::drawPhase(2000,ui::PH_INHALE,0.55f,2,0,4,0,false);});
  frame("hold.raw",[]{ui::drawPhase(9000,ui::PH_HOLD,0.4f,5,1,4,0,false);});
  frame("exhale.raw",[]{ui::drawPhase(14000,ui::PH_EXHALE,0.3f,6,2,4,70,true);});
  frame("done.raw",[]{ui::drawDone(2000,85,13);});
  frame("stopped.raw",[]{ui::drawStopped(100);});
  auto t=std::chrono::steady_clock::now(); for(int i=0;i<50;i++) frame("x.raw",[]{ui::drawPhase(14000,ui::PH_EXHALE,0.3f,6,2,4,70,true);});
  printf("host frame %.2f ms\n", std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-t).count()/50);
}
