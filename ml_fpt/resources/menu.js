﻿{
    var l_block = document.createElement('div');
    l_block.innerHTML = `
        <h2>4-Point Tracking</h2>
        <div class ="action-btn" onclick="engine.trigger('MelonMod_FPT_Action_Calibrate');"><img src="gfx/recalibrate.svg">Calibrate</div>
    `;
    document.getElementById('settings-implementation').appendChild(l_block);
}