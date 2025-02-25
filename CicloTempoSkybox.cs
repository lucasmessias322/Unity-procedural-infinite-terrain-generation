using UnityEngine;

public class CicloTempoSkybox : MonoBehaviour
{
    [Header("Configurações do Ciclo de Tempo")]
    [Tooltip("Duração completa do ciclo em segundos (ex.: 120 para 2 minutos)")]
    public float duracaoDoCiclo = 120f;
    private float tempoAtual = 0f; // tempo acumulado no ciclo

    [Header("Skybox")]
    [Tooltip("Material do Skybox para o dia")]
    public Material skyboxDia;
    [Tooltip("Material do Skybox para a tarde")]
    public Material skyboxTarde;
    [Tooltip("Material do Skybox para a noite")]
    public Material skyboxNoite;

    [Header("Luz Direcional")]
    [Tooltip("Luz direcional que será ajustada")]
    public Light directionalLight;
    [Tooltip("Cor da luz durante o dia")]
    public Color corDia = Color.white;
    [Tooltip("Cor da luz durante a tarde")]
    public Color corTarde = new Color(1f, 0.8f, 0.6f); // exemplo: tom mais quente
    [Tooltip("Cor da luz durante a noite")]
    public Color corNoite = Color.blue;
    [Tooltip("Intensidade da luz durante o dia")]
    public float intensidadeDia = 1f;
    [Tooltip("Intensidade da luz durante a tarde")]
    public float intensidadeTarde = 0.8f;
    [Tooltip("Intensidade da luz durante a noite")]
    public float intensidadeNoite = 0.2f;

    [Header("Fog Linear")]
    [Tooltip("Cor do fog durante o dia")]
    public Color corFogDia = new Color(0.7f, 0.8f, 0.9f);
    [Tooltip("Distância inicial do fog durante o dia")]
    public float fogStartDia = 50f;
    [Tooltip("Distância final do fog durante o dia")]
    public float fogEndDia = 200f;

    [Tooltip("Cor do fog durante a tarde")]
    public Color corFogTarde = new Color(0.8f, 0.7f, 0.6f);
    [Tooltip("Distância inicial do fog durante a tarde")]
    public float fogStartTarde = 40f;
    [Tooltip("Distância final do fog durante a tarde")]
    public float fogEndTarde = 150f;

    [Tooltip("Cor do fog durante a noite")]
    public Color corFogNoite = new Color(0.1f, 0.1f, 0.2f);
    [Tooltip("Distância inicial do fog durante a noite")]
    public float fogStartNoite = 20f;
    [Tooltip("Distância final do fog durante a noite")]
    public float fogEndNoite = 100f;

    [Header("Horários")]
    [Range(0, 24)]
    [Tooltip("Hora do amanhecer (ex.: 6)")]
    public float horaAmanhecer = 6f;
    [Range(0, 24)]
    [Tooltip("Hora de início da tarde (ex.: 12)")]
    public float horaInicioTarde = 12f;
    [Range(0, 24)]
    [Tooltip("Hora do anoitecer (ex.: 18)")]
    public float horaAnoitecer = 18f;

    void Start()
    {
        // Ativa o fog, caso ainda não esteja ativado
        RenderSettings.fog = true;
        // Inicia o ciclo como meio-dia
        tempoAtual = (12f / 24f) * duracaoDoCiclo;
    }

    void Update()
    {
        // Atualiza o tempo do ciclo
        tempoAtual += Time.deltaTime;
        if (tempoAtual > duracaoDoCiclo)
            tempoAtual -= duracaoDoCiclo;

        // Converte o tempo atual para "hora do dia" (0 a 24 horas)
        float horaDoDia = (tempoAtual / duracaoDoCiclo) * 24f;

        // Variáveis para armazenar os valores atuais de cor, intensidade, skybox e fog
        Color corAtual, corFogAtual;
        float intensidadeAtual, fogStartAtual, fogEndAtual;
        Material skyboxAtual;

        // Define as fases do dia e as transições (cada transição dura 1 hora)
        if (horaDoDia < horaAmanhecer)
        {
            // Antes do amanhecer: noite completa
            corAtual = corNoite;
            intensidadeAtual = intensidadeNoite;
            skyboxAtual = skyboxNoite;

            corFogAtual = corFogNoite;
            fogStartAtual = fogStartNoite;
            fogEndAtual = fogEndNoite;
        }
        else if (horaDoDia < horaAmanhecer + 1f)
        {
            // Transição do amanhecer: de noite para dia
            float t = (horaDoDia - horaAmanhecer) / 1f;
            corAtual = Color.Lerp(corNoite, corDia, t);
            intensidadeAtual = Mathf.Lerp(intensidadeNoite, intensidadeDia, t);
            skyboxAtual = t < 0.5f ? skyboxNoite : skyboxDia;

            corFogAtual = Color.Lerp(corFogNoite, corFogDia, t);
            fogStartAtual = Mathf.Lerp(fogStartNoite, fogStartDia, t);
            fogEndAtual = Mathf.Lerp(fogEndNoite, fogEndDia, t);
        }
        else if (horaDoDia < horaInicioTarde)
        {
            // Dia completo (manhã)
            corAtual = corDia;
            intensidadeAtual = intensidadeDia;
            skyboxAtual = skyboxDia;

            corFogAtual = corFogDia;
            fogStartAtual = fogStartDia;
            fogEndAtual = fogEndDia;
        }
        else if (horaDoDia < horaInicioTarde + 1f)
        {
            // Transição do dia para a tarde
            float t = (horaDoDia - horaInicioTarde) / 1f;
            corAtual = Color.Lerp(corDia, corTarde, t);
            intensidadeAtual = Mathf.Lerp(intensidadeDia, intensidadeTarde, t);
            skyboxAtual = t < 0.5f ? skyboxDia : skyboxTarde;

            corFogAtual = Color.Lerp(corFogDia, corFogTarde, t);
            fogStartAtual = Mathf.Lerp(fogStartDia, fogStartTarde, t);
            fogEndAtual = Mathf.Lerp(fogEndDia, fogEndTarde, t);
        }
        else if (horaDoDia < horaAnoitecer)
        {
            // Tarde completa
            corAtual = corTarde;
            intensidadeAtual = intensidadeTarde;
            skyboxAtual = skyboxTarde;

            corFogAtual = corFogTarde;
            fogStartAtual = fogStartTarde;
            fogEndAtual = fogEndTarde;
        }
        else if (horaDoDia < horaAnoitecer + 1f)
        {
            // Transição da tarde para a noite (anoitecer)
            float t = (horaDoDia - horaAnoitecer) / 1f;
            corAtual = Color.Lerp(corTarde, corNoite, t);
            intensidadeAtual = Mathf.Lerp(intensidadeTarde, intensidadeNoite, t);
            skyboxAtual = t < 0.5f ? skyboxTarde : skyboxNoite;

            corFogAtual = Color.Lerp(corFogTarde, corFogNoite, t);
            fogStartAtual = Mathf.Lerp(fogStartTarde, fogStartNoite, t);
            fogEndAtual = Mathf.Lerp(fogEndTarde, fogEndNoite, t);
        }
        else
        {
            // Após o anoitecer: noite completa
            corAtual = corNoite;
            intensidadeAtual = intensidadeNoite;
            skyboxAtual = skyboxNoite;

            corFogAtual = corFogNoite;
            fogStartAtual = fogStartNoite;
            fogEndAtual = fogEndNoite;
        }

        // Atualiza o Skybox
        RenderSettings.skybox = skyboxAtual;

        // Atualiza a luz direcional (intensidade e cor)
        if (directionalLight != null)
        {
            directionalLight.intensity = intensidadeAtual;
            directionalLight.color = corAtual;
        }

        // Atualiza o Fog Linear (cor, distância inicial e final)
        RenderSettings.fogColor = corFogAtual;
        RenderSettings.fogStartDistance = fogStartAtual;
        RenderSettings.fogEndDistance = fogEndAtual;
    }
}
