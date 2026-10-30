#include "breez_ui.h"
#include <cstdio>
#include <chrono>
static uint16_t FB[240*240];
void save(const char* n){ FILE*f=fopen(n,"wb"); fwrite(FB,2,240*240,f); fclose(f); }
int main(){
  ui::begin(FB);
  ui::drawIdle(1500, 0, 12); save("idle.raw");
  ui::drawIdle(3000, 60, 12); save("idle_blow.raw");
  ui::drawCalibrating(300); save("cal.raw");
  ui::drawReady(3, 0.1f); save("ready.raw");
  ui::drawPhase(ui::PH_INHALE, 0.55f, 2, 0, 4, 0, false); save("inhale.raw");
  ui::drawPhase(ui::PH_HOLD, 0.4f, 5, 1, 4, 0, false); save("hold.raw");
  ui::drawPhase(ui::PH_EXHALE, 0.3f, 6, 2, 4, 70, true); save("exhale.raw");
  ui::drawDone(2000, 85, 13); save("done.raw");
  ui::drawStopped(); save("stopped.raw");
  auto t=std::chrono::steady_clock::now(); for(int i=0;i<100;i++) ui::drawPhase(ui::PH_EXHALE, 0.3f, 6, 2, 4, 70, true);
  printf("host frame %.2f ms\n", std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-t).count()/100);
}
