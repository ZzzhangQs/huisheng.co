# 洄生 · 植物范围触发音效

打开 PlantMusicDemo.unity，点击 Unity 播放。
用 WASD 或方向键移动棕色圆点，进入植物脚下的感应圈。

- 角色进入时，仅对应植物播放一次音效。
- 停留在感应圈内不会重复播放。
- 离开后再次进入会重新触发。
- 四种植物分别使用钢琴、笛子、木琴、低音弦乐风格的合成音。
- 没有自动合奏或循环播放。
- 界面仅保留移动提示与当前触发植物名称，音量可在 Inspector 设置。

PlantProximitySound：每株植物独立的 CircleCollider2D 感应范围、AudioSource 和音效。
PlantDemoPlayer：演示角色移动与 Rigidbody2D。
PlantMusicGarden：生成演示场景内容与说明界面。
在 Plant Music Garden 的 Plant Sounds 数组指定四株植物的替代音效，留空则使用默认合成音。

验证：启动不发声；首次进入花朵范围计数为 1，停留保持 1；离开重进变为 2；其余三株依次靠近后各为 1。四个声源循环均关闭，运行无错误。
演示中文字体来自本机 Arial Unicode；分发前请换成具有相应分发许可的字体。

## 植物视觉更新
四种植物已替换为参考微缩景观方向生成的透明植物图集 BotanicalAtlas.png：白色钟形花丛、竹丛、苔藓菌群、自然枝干小乔木。图集为二维透明素材，体积与光照已绘制在图像中。使用内置图像生成工具制作；提示词保存在工作区 outputs/PlantMusicDemo/plant-art-prompt.txt。原始植物素材保留。界面移除了大按钮，感应圈仅在角色进入时淡显。
