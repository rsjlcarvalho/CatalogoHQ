﻿using CatalogoHQ.Controllers;
using CatalogoHQ.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

namespace CatalogoHQ.Repository
{
    public class PersonagemRepositorio
    {
        HttpClient cliente = new HttpClient();
        private string chavePublica;
        private string chavePrivada;
        private string hash;
        private string ts;

        public PersonagemRepositorio(IConfiguration configuracao)
        {
            cliente.DefaultRequestHeaders.Accept.Clear();
            cliente.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            ts = DateTime.Now.Ticks.ToString();
            chavePublica = configuracao.GetSection("MarvelComicsAPI:PublicKey").Value;
            chavePrivada = configuracao.GetSection("MarvelComicsAPI:PrivateKey").Value;

            var hashController = new HashController();
            hash = hashController.GerarHash(ts, chavePublica, chavePrivada);
        }

        public int ObterTotalPersonagens(IConfiguration configuracao)
        {
            HttpResponseMessage reposta = cliente.GetAsync(
                configuracao.GetSection("MarvelComicsAPI:RequestURL").Value +
                $"characters?ts={ts}&apikey={chavePublica}&hash={hash}&limit={1}&offset={1}").Result;

            reposta.EnsureSuccessStatusCode();
            string conteudo = reposta.Content.ReadAsStringAsync().Result;

            dynamic resultado = JsonConvert.DeserializeObject(conteudo);

            return resultado.data.total;
        }

        public List<Personagem> ObterPersonagens(IConfiguration configuracao, IMemoryCache cache)
        {
            var total = ObterTotalPersonagens(configuracao);
            var max = (int)Enums.Enums.LimitePersonagem.Maximo;
            var lsPersonagens = new List<Personagem>();

            // Add cache
            var personagens = cache.GetOrCreate(
                "ListaPersonagens", context =>
                {
                    // Expira em 10h - max recomendado pela Marvel 24h
                    context.SetAbsoluteExpiration(TimeSpan.FromHours(10));
                    context.SetPriority(CacheItemPriority.High);

                    while (total > lsPersonagens.Count)
                    {
                        HttpResponseMessage resposta = cliente.GetAsync(
                                configuracao.GetSection("MarvelComicsAPI:RequestURL").Value +
                                $"characters?ts={ts}&apikey={chavePublica}&hash={hash}&limit={max}&offset={lsPersonagens.Count}").Result;

                        resposta.EnsureSuccessStatusCode();
                        string conteudo = resposta.Content.ReadAsStringAsync().Result;

                        dynamic resultado = JsonConvert.DeserializeObject(conteudo);

                        for (var x = 0; x < (int)resultado.data.count; x++)
                        {
                            Personagem personagem = new Personagem
                            {
                                Id = resultado.data.results[x].id,
                                Nome = resultado.data.results[x].name
                            };

                            lsPersonagens.Add(personagem);
                        }

                    }
                    return lsPersonagens;
                });

            return personagens;
        }

        public Personagem ObterPersonagem(IConfiguration configuracao, int id)
        {
            HttpResponseMessage reposta = cliente.GetAsync(
                configuracao.GetSection("MarvelComicsAPI:RequestURL").Value +
                $"characters/{id}?ts={ts}&apikey={chavePublica}&hash={hash}").Result;

            reposta.EnsureSuccessStatusCode();
            string conteudo = reposta.Content.ReadAsStringAsync().Result;

            dynamic resultado = JsonConvert.DeserializeObject(conteudo);

            Personagem personagem = new Personagem
            {
                Id = resultado.data.results[0].id,
                Nome = resultado.data.results[0].name,
                Descricao = resultado.data.results[0].description,
                ImagemUrl = resultado.data.results[0].thumbnail.path + "/portrait_xlarge." + resultado.data.results[0].thumbnail.extension
            };

            return personagem;
        }
    }
}
