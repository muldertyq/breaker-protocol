shader_type canvas_item;

// ==========================================
// 三通道调色板参数 (Uniforms)
// ==========================================
uniform vec4 u_primary_color : source_color = vec4(0.227, 0.247, 0.278, 1.0);  // 主装甲色
uniform vec4 u_secondary_color : source_color = vec4(0.847, 0.490, 0.141, 1.0); // 副战术色
uniform vec4 u_accent_color : source_color = vec4(0.314, 0.710, 1.0, 1.0);     // 能量发光色
uniform vec4 u_raw_metal_color : source_color = vec4(0.353, 0.365, 0.392, 1.0); // 生铁底色

// ==========================================
// 动态战损与过热参数
// ==========================================
uniform float u_health_ratio : hint_range(0.0, 1.0) = 1.0; // 构件血量比例 (1.0 = 满血, 0.0 = 濒死)
uniform float u_overheat_ratio : hint_range(0.0, 1.0) = 0.0; // 过热度 (0.0 = 常温, 1.0 = 极热)
uniform float u_damage_scorch : hint_range(0.0, 1.0) = 0.0;  // 焦黑灼烧程度

// 伪随机噪点生成函数 (基于像素 UV)
float pseudo_noise(vec2 co) {
    return fract(sin(dot(co.xy, vec2(12.9898, 78.233))) * 43758.5453);
}

void fragment() {
    vec4 base_tex = texture(TEXTURE, UV);
    if (base_tex.a < 0.01) {
        discard;
    }

    float gray = base_tex.r; // 读取中性灰度值 (0.0 ~ 1.0)
    vec4 final_color;

    // ------------------------------------------------------------
    // 阶段 1：四阶灰度区间映射 (规范 07)
    // ------------------------------------------------------------
    // 1. 结构阴影与铆钉焊缝区 (0 ~ 60 / 255 ≈ 0.0 ~ 0.235)
    if (gray <= 0.235) {
        final_color = vec4(vec3(gray * 1.25), base_tex.a);
    }
    // 2. 主装甲大板区 (61 ~ 160 / 255 ≈ 0.235 ~ 0.627)
    else if (gray <= 0.627) {
        float factor = (gray - 0.235) / (0.627 - 0.235);
        vec3 col = u_primary_color.rgb * (0.65 + 0.35 * factor);
        final_color = vec4(col, base_tex.a);
    }
    // 3. 副战术拉花条纹区 (161 ~ 220 / 255 ≈ 0.627 ~ 0.863)
    else if (gray <= 0.863) {
        float factor = (gray - 0.627) / (0.863 - 0.627);
        vec3 col = u_secondary_color.rgb * (0.80 + 0.20 * factor);
        final_color = vec4(col, base_tex.a);
    }
    // 4. 金属边缘高光区 (221 ~ 255 / 255 ≈ 0.863 ~ 1.0)
    else {
        float highlight = (gray - 0.863) / (1.0 - 0.863);
        vec3 col = mix(u_primary_color.rgb, vec3(1.0), highlight * 0.75);
        final_color = vec4(col, base_tex.a);
    }

    // ------------------------------------------------------------
    // 阶段 2：战损掉漆噪点算法 (HP < 60% 时漆面剥落露出生铁)
    // ------------------------------------------------------------
    if (u_health_ratio < 0.60 && gray > 0.235) {
        // 使用离散像素 UV 生成噪点
        vec2 pixel_uv = floor(UV * 24.0) / 24.0;
        float noise = pseudo_noise(pixel_uv + vec2(u_health_ratio * 0.1, 0.0));
        
        // 当噪点阈值超过当前血量比例时，剥离涂装
        if (noise > (u_health_ratio / 0.60)) {
            final_color.rgb = u_raw_metal_color.rgb * (0.7 + 0.3 * gray);
        }
    }

    // ------------------------------------------------------------
    // 阶段 3：受创焦黑灼烧 (Damage Scorch)
    // ------------------------------------------------------------
    if (u_damage_scorch > 0.0) {
        vec3 scorch_color = vec3(0.08, 0.08, 0.10); // 炭黑焦痕
        final_color.rgb = mix(final_color.rgb, scorch_color, u_damage_scorch * 0.85);
    }

    // ------------------------------------------------------------
    // 阶段 4：高温红热与过载泛光 (Overheat Heat-tint)
    // ------------------------------------------------------------
    if (u_overheat_ratio > 0.0) {
        vec3 heat_color = vec3(1.0, 0.22, 0.05); // 炽热橙红
        float heat_intensity = u_overheat_ratio * u_overheat_ratio;
        final_color.rgb = mix(final_color.rgb, heat_color, heat_intensity * 0.75);
    }

    COLOR = final_color;
}